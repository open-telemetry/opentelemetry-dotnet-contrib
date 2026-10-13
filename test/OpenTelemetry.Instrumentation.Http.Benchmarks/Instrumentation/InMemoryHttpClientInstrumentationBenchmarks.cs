// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Benchmarks.Instrumentation;

/// <summary>
/// Measures the per-request overhead of the HttpClient instrumentation without
/// any network or server noise. A <see cref="SocketsHttpHandler"/> is used (so
/// that the runtime's DiagnosticsHandler is part of the handler chain) whose
/// connections are in-memory streams that reply to every request with an empty
/// <c>200 OK</c> response.
/// </summary>
[MemoryDiagnoser]
public class InMemoryHttpClientInstrumentationBenchmarks
{
    private static readonly Uri Url = new("http://inmemory.test/api/orders?page=2&size=50");

    private HttpClient? httpClient;
    private TracerProvider? tracerProvider;

    [Params(false, true)]
    public bool Traces { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (this.Traces)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .Build();
        }

        var handler = new SocketsHttpHandler()
        {
            ConnectCallback = static (_, _) => ValueTask.FromResult<Stream>(new InMemoryHttpConnectionStream()),
            UseProxy = false,
        };

        this.httpClient = new HttpClient(handler);

        // Send a probe request to check that the in-memory connection works and that the
        // runtime only injects the trace context headers when the instrumentation is enabled.
        using var request = new HttpRequestMessage(HttpMethod.Get, Url);
        using var response = this.httpClient.Send(request);

        response.EnsureSuccessStatusCode();

        if (request.Headers.Contains("traceparent") != this.Traces)
        {
            throw new InvalidOperationException($"The HttpClient instrumentation is not behaving as expected when {nameof(this.Traces)} is {this.Traces}.");
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.httpClient?.Dispose();
        this.tracerProvider?.Dispose();
    }

    [Benchmark]
    public async Task HttpClientRequest()
    {
        using var response = await this.httpClient!.GetAsync(Url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A fake HTTP/1.1 connection that answers each request (a request is assumed
    /// to end with an empty line, so requests must not have a body) with an empty
    /// 200 response.
    /// </summary>
    private sealed class InMemoryHttpConnectionStream : Stream
    {
        private static readonly byte[] Response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray();

        private readonly SemaphoreSlim responseAvailable = new(0);
        private int terminatorMatched;
        private int responseOffset = -1;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count)
            => this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.responseOffset < 0)
            {
                await this.responseAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (buffer.Length == 0)
                {
                    // Zero-byte read used to wait for data: leave the response for the next read.
                    this.responseAvailable.Release();
                    return 0;
                }

                this.responseOffset = 0;
            }

            if (buffer.Length == 0)
            {
                return 0;
            }

            var offset = this.responseOffset;
            var count = Math.Min(buffer.Length, Response.Length - offset);
            Response.AsSpan(offset, count).CopyTo(buffer.Span);

            offset += count;
            this.responseOffset = offset == Response.Length ? -1 : offset;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            foreach (var b in buffer)
            {
                var expected = (this.terminatorMatched % 2 == 0) ? (byte)'\r' : (byte)'\n';

                if (b == expected)
                {
                    if (++this.terminatorMatched == 4)
                    {
                        this.terminatorMatched = 0;
                        this.responseAvailable.Release();
                    }
                }
                else
                {
                    this.terminatorMatched = b == '\r' ? 1 : 0;
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.Write(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.Write(buffer.Span);
            return default;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.responseAvailable.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
