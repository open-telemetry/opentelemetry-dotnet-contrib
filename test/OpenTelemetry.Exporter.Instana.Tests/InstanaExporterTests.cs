// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public class InstanaExporterTests
{
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Export()
    {
        // Arrange
        var startedUtc = DateTimeOffset.FromUnixTimeMilliseconds(1776522506123);
        var utcNow = DateTimeOffset.FromUnixTimeMilliseconds(1776523555069);

        var options = new InstanaExporterOptions()
        {
            AgentKey = Guid.NewGuid().ToString(),
            GetParentProviderResource = (_) =>
            {
                return new Resource(
                [
                    new("service.name", "serviceName"),
                    new("service.instance.id", "serviceInstanceId"),
                    new("process.pid", "processPid"),
                    new("host.id", "hostId"),
                ]);
            },
            UtcNow = () => utcNow,
        };

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = TestHttpServer.RunServer(
            (context) =>
            {
                try
                {
                    Assert.Equal("POST", context.Request.HttpMethod);
                    Assert.Equal(new(options.EndpointUri, "/bundle"), context.Request.Url);

                    Assert.Equal("application/json", context.Request.Headers["Content-Type"]);
                    Assert.Equal(options.AgentKey, context.Request.Headers["X-INSTANA-KEY"]);
                    Assert.Equal("1", context.Request.Headers["X-INSTANA-NOTRACE"]);
                    Assert.Equal("1776523555069", context.Request.Headers["X-INSTANA-TIME"]);

                    using var reader = new StreamReader(context.Request.InputStream);
                    tcs.SetResult(reader.ReadToEnd());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            },
            out var host,
            out var port);

        options.EndpointUri = new UriBuilder(Uri.UriSchemeHttp, host, port).Uri;
        options.ProxyUri = options.EndpointUri;

        var spans = new List<InstanaSpan>();
        var processor = new TestActivityProcessor((activity, span) =>
        {
            Assert.NotNull(activity);
            spans.Add(span);
        });

        using var exporter = new InstanaExporter(options, processor);

        using var activity = new Activity("testOperationName");
        activity.SetStartTime(startedUtc.UtcDateTime);
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");
        activity.TraceStateString = "TraceStateString";

        Activity[] activities = [activity];
        var batch = new Batch<Activity>(activities, activities.Length);

        // Act
        var result = exporter.Export(batch);

        // Assert
        Assert.Equal(ExportResult.Success, result);

        var actual = await WaitForExportAsync(tcs);

        using var document = JsonDocument.Parse(actual);
        Assert.NotNull(document);

        var exportedSpans = document.RootElement.GetProperty("spans");
        var exportedSpan = exportedSpans.EnumerateArray().Single();

        Assert.Equal("0000000000000000", exportedSpan.GetProperty("t").GetString());
        Assert.Equal("0000000000000000", exportedSpan.GetProperty("s").GetString());
        Assert.Equal("00000000000000000000000000000000", exportedSpan.GetProperty("lt").GetString());
        Assert.Equal("3", exportedSpan.GetProperty("k").GetString());
        Assert.Equal("otel", exportedSpan.GetProperty("n").GetString());
        Assert.Equal(1776522506123, exportedSpan.GetProperty("ts").GetInt64());
        Assert.Equal(0, exportedSpan.GetProperty("d").GetInt32());
        Assert.Equal(1, exportedSpan.GetProperty("ec").GetInt32());

        var from = exportedSpan.GetProperty("f");
        var data = exportedSpan.GetProperty("data");

        Assert.Equal("internal", data.GetProperty("kind").GetString());
        Assert.Equal("Error", data.GetProperty("error").GetString());
        Assert.Equal("TestErrorDesc", data.GetProperty("error_detail").GetString());
        Assert.Equal("serviceName", data.GetProperty("service").GetString());
        Assert.Equal("testOperationName", data.GetProperty("operation").GetString());
        Assert.Equal("TraceStateString", data.GetProperty("trace_state").GetString());

        var instanaSpan = Assert.Single(spans);

        Assert.NotNull(instanaSpan);
        Assert.Equal("serviceName", instanaSpan.Data.Values["service"]);
        Assert.Equal("testOperationName", instanaSpan.Data.Values["operation"]);
        Assert.Equal("TraceStateString", instanaSpan.Data.Values["trace_state"]);

#if NETFRAMEWORK
        using var process = Process.GetCurrentProcess();

        string expectedPid = process.Id.ToString(CultureInfo.InvariantCulture);
        string expectedHostId = string.Empty;
#else
        string expectedPid = OperatingSystem.IsWindows() ?
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture) :
            "processPid";

        string expectedHostId = OperatingSystem.IsWindows() ?
            string.Empty :
            "hostId";
#endif

        Assert.Equal(expectedPid, instanaSpan.F.E);
        Assert.Equal(expectedPid, from.GetProperty("e").GetString());

        Assert.Equal(expectedHostId, instanaSpan.F.H);
    }

    [Fact]
    public async Task Export_ProcessPidDoesNotExistButServiceIdExists()
    {
        // Arrange
        var startedUtc = DateTimeOffset.FromUnixTimeMilliseconds(1776522506123);
        var utcNow = DateTimeOffset.FromUnixTimeMilliseconds(1776523555069);

        var options = new InstanaExporterOptions()
        {
            AgentKey = Guid.NewGuid().ToString(),
            GetParentProviderResource = (_) =>
            {
                return new Resource(
                [
                    new("service.name", "serviceName"),
                    new("service.instance.id", "serviceInstanceId"),
                    new("host.id", "hostId"),
                ]);
            },
            UtcNow = () => utcNow,
        };

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = TestHttpServer.RunServer(
            (context) =>
            {
                try
                {
                    Assert.Equal("POST", context.Request.HttpMethod);
                    Assert.Equal(new(options.EndpointUri, "/bundle"), context.Request.Url);

                    Assert.Equal("application/json", context.Request.Headers["Content-Type"]);
                    Assert.Equal(options.AgentKey, context.Request.Headers["X-INSTANA-KEY"]);
                    Assert.Equal("1", context.Request.Headers["X-INSTANA-NOTRACE"]);
                    Assert.Equal("1776523555069", context.Request.Headers["X-INSTANA-TIME"]);

                    using var reader = new StreamReader(context.Request.InputStream);
                    tcs.SetResult(reader.ReadToEnd());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            },
            out var host,
            out var port);

        options.EndpointUri = new UriBuilder(Uri.UriSchemeHttp, host, port).Uri;

        var spans = new List<InstanaSpan>();

        var processor = new TestActivityProcessor((activity, span) =>
        {
            Assert.NotNull(activity);
            spans.Add(span);
        });

        using var exporter = new InstanaExporter(options, processor);

        using var activity = new Activity("testOperationName");
        activity.SetStartTime(startedUtc.UtcDateTime);
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");

        Activity[] activities = [activity];
        var batch = new Batch<Activity>(activities, activities.Length);

        // Act
        var result = exporter.Export(batch);

        // Assert
        Assert.Equal(ExportResult.Success, result);

        var actual = await WaitForExportAsync(tcs);

        using var document = JsonDocument.Parse(actual);
        Assert.NotNull(document);

        var exportedSpans = document.RootElement.GetProperty("spans");
        var exportedSpan = exportedSpans.EnumerateArray().Single();

        Assert.Equal("0000000000000000", exportedSpan.GetProperty("t").GetString());
        Assert.Equal("0000000000000000", exportedSpan.GetProperty("s").GetString());
        Assert.Equal("00000000000000000000000000000000", exportedSpan.GetProperty("lt").GetString());
        Assert.Equal("3", exportedSpan.GetProperty("k").GetString());
        Assert.Equal("otel", exportedSpan.GetProperty("n").GetString());
        Assert.Equal(1776522506123, exportedSpan.GetProperty("ts").GetInt64());
        Assert.Equal(0, exportedSpan.GetProperty("d").GetInt32());
        Assert.Equal(1, exportedSpan.GetProperty("ec").GetInt32());

        var from = exportedSpan.GetProperty("f");
        var data = exportedSpan.GetProperty("data");

        Assert.Equal("internal", data.GetProperty("kind").GetString());
        Assert.Equal("Error", data.GetProperty("error").GetString());
        Assert.Equal("TestErrorDesc", data.GetProperty("error_detail").GetString());
        Assert.Equal("serviceName", data.GetProperty("service").GetString());
        Assert.Equal("testOperationName", data.GetProperty("operation").GetString());

        var instanaSpan = Assert.Single(spans);

        Assert.NotNull(instanaSpan);
        Assert.Equal("serviceName", instanaSpan.Data.Values["service"]);
        Assert.Equal("testOperationName", instanaSpan.Data.Values["operation"]);

#if NETFRAMEWORK
        using var process = Process.GetCurrentProcess();

        string expectedPid = process.Id.ToString(CultureInfo.InvariantCulture);
        string expectedHostId = string.Empty;
#else
        string expectedPid = OperatingSystem.IsWindows() ?
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture) :
            "serviceInstanceId";

        string expectedHostId = OperatingSystem.IsWindows() ?
            string.Empty :
            "hostId";
#endif

        Assert.Equal(expectedPid, instanaSpan.F.E);
        Assert.Equal(expectedPid, from.GetProperty("e").GetString());

        Assert.Equal(expectedHostId, instanaSpan.F.H);
    }

    [Fact]
    public void Export_ExporterIsShutDown()
    {
        // Arrange
        var options = new InstanaExporterOptions()
        {
            AgentKey = Guid.NewGuid().ToString(),
            EndpointUri = new Uri("http://localhost:42699"),
        };

        var spans = new List<InstanaSpan>();
        var processor = new TestActivityProcessor((activity, span) =>
        {
            Assert.NotNull(activity);
            spans.Add(span);
        });

        using var exporter = new InstanaExporter(options, processor);

        using var activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");

        exporter.Shutdown();

        var batch = new Batch<Activity>([activity], 1);

        // Act
        var actual = exporter.Export(batch);

        // Assert
        Assert.Equal(ExportResult.Failure, actual);
        Assert.Empty(spans);
    }

    [Fact]
    public async Task Export_WithCustomHttpClient()
    {
        // Arrange
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var handler = new TestHttpMessageHandler(tcs);
        using var httpClient = new HttpClient(handler);

        var options = new InstanaExporterOptions()
        {
            AgentKey = "instana-agent-key",
            EndpointUri = new Uri("http://localhost:42699"),
            HttpClientFactory = () => httpClient,
        };

        var processor = DefaultActivityProcessor.CreateDefault();

        using var exporter = new InstanaExporter(options, processor);

        using var activity = new Activity("my-operation");

        Activity[] activities = [activity];
        var batch = new Batch<Activity>(activities, activities.Length);

        // Act
        var result = exporter.Export(batch);

        // Assert
        Assert.Equal(ExportResult.Success, result);

        var actual = await WaitForExportAsync(tcs);

        var exception = Record.Exception(() => JsonDocument.Parse(actual));
        Assert.Null(exception);

        Assert.Equal(1, handler.InvocationCount);
    }

    private static async Task<string> WaitForExportAsync(TaskCompletionSource<string> completionSource)
    {
        var timeout = ExportTimeout;

#if NET
        await completionSource.Task.WaitAsync(timeout);
#else
        using var cts = new CancellationTokenSource(timeout);
        var completed = await Task.WhenAny(completionSource.Task, Task.Delay(timeout, cts.Token));
        Assert.Same(completionSource.Task, completed);
#endif

        return await completionSource.Task;
    }

    private sealed class TestActivityProcessor : ActivityProcessorBase
    {
        private readonly Action<Activity, InstanaSpan> callback;

        public TestActivityProcessor(Action<Activity, InstanaSpan> callback)
        {
            this.callback = callback;
            this.NextProcessor = DefaultActivityProcessor.CreateDefault();
        }

        public override void Process(Activity activity, InstanaSpan instanaSpan)
        {
            base.Process(activity, instanaSpan);
            this.callback(activity, instanaSpan);
        }
    }

    private sealed class TestHttpMessageHandler(TaskCompletionSource<string> completionSource) : HttpMessageHandler
    {
        public int InvocationCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.InvocationCount++;

#if NETFRAMEWORK
            using (var stream = await request.Content!.ReadAsStreamAsync())
#else
            using (var stream = await request.Content!.ReadAsStreamAsync(cancellationToken))
#endif
            using (var reader = new StreamReader(stream))
            {
#if NETFRAMEWORK
                completionSource.SetResult(await reader.ReadToEndAsync());
#else
                completionSource.SetResult(await reader.ReadToEndAsync(cancellationToken));
#endif
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.InvocationCount++;

            using (var stream = request.Content!.ReadAsStream(cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                completionSource.SetResult(reader.ReadToEnd());
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
#endif
    }
}
