// <copyright file="LogRecordCommonSchemaJsonHttpPostBenchmarks.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OneCollector.Benchmarks;

[MemoryDiagnoser]
public class LogRecordCommonSchemaJsonHttpPostBenchmarks
{
    private static readonly MethodInfo LogRecordSetScopeProviderMethodInfo = typeof(LogRecord).GetProperty("ScopeProvider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetMethod
        ?? throw new InvalidOperationException("LogRecord.ScopeProvider.Set could not be found reflectively.");

    private LogRecord[]? logRecords;
    private OneCollectorExporter<LogRecord>? exporter;

    [Params(1, 10, 100)]
    public int NumberOfBatches { get; set; }

    [Params(1000, 10_000)]
    public int NumberOfLogRecordsPerBatch { get; set; }

    [Params(true, false)]
    public bool EnableCompression { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.logRecords = new LogRecord[this.NumberOfLogRecordsPerBatch];

        for (int i = 0; i < this.NumberOfLogRecordsPerBatch; i++)
        {
            this.logRecords[i] = CreateLogRecord(i);
        }

        var exporterOptions = new OneCollectorLogExporterOptions
        {
            ConnectionString = "InstrumentationKey=token-extrainformation",
        };

        exporterOptions.Validate();

        var transportOptions = exporterOptions.TransportOptions;

        transportOptions.HttpCompression = this.EnableCompression
            ? OneCollectorExporterHttpTransportCompressionType.Deflate
            : OneCollectorExporterHttpTransportCompressionType.None;

        var sink = new WriteDirectlyToTransportSink<LogRecord>(
            new LogRecordCommonSchemaJsonSerializer(
                new EventNameManager(exporterOptions.DefaultEventNamespace, exporterOptions.DefaultEventName),
                exporterOptions.TenantToken!,
                exporterOptions.SerializationOptions.ExceptionStackTraceHandling,
                transportOptions.MaxPayloadSizeInBytes == -1 ? int.MaxValue : transportOptions.MaxPayloadSizeInBytes,
                transportOptions.MaxNumberOfItemsPerPayload == -1 ? int.MaxValue : transportOptions.MaxNumberOfItemsPerPayload),
            new HttpJsonPostTransport(
                exporterOptions.InstrumentationKey!,
                transportOptions.Endpoint,
                transportOptions.HttpCompression,
                new NoopHttpClient()));

        this.exporter = new OneCollectorExporter<LogRecord>(sink);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.exporter?.Dispose();
    }

    [Benchmark]
    public void Export()
    {
        for (int i = 0; i < this.NumberOfBatches; i++)
        {
            this.exporter!.Export(new Batch<LogRecord>(this.logRecords!, this.logRecords!.Length));
        }
    }

    private static LogRecord CreateLogRecord(int index)
    {
        var logRecord = (LogRecord)Activator.CreateInstance(typeof(LogRecord), nonPublic: true)!;

        logRecord.Timestamp = DateTime.UtcNow;
        logRecord.CategoryName = typeof(LogRecordCommonSchemaJsonHttpPostBenchmarks).FullName;
        logRecord.LogLevel = LogLevel.Information;

        if (index % 2 == 0)
        {
            if (index % 4 == 0)
            {
                logRecord.EventId = new EventId(2, "MyEvent");
            }
            else
            {
                logRecord.EventId = new EventId(1);
            }

            logRecord.Attributes = new List<KeyValuePair<string, object?>>
            {
                new KeyValuePair<string, object?>("userId", 18),
                new KeyValuePair<string, object?>("greeting", "hello world"),
                new KeyValuePair<string, object?>("{OriginalFormat}", "Structured logging {userId} {greeting}"),
            };

            if (index % 3 == 0)
            {
                var scopeProvider = new ScopeProvider(
                    new List<KeyValuePair<string, object?>> { new KeyValuePair<string, object?>("scope1Key1", "scope1Value1"), new KeyValuePair<string, object?>("scope1Key2", "scope1Value2") },
                    new List<KeyValuePair<string, object?>> { new KeyValuePair<string, object?>("scope2Key1", "scope2Value1") });

                LogRecordSetScopeProviderMethodInfo.Invoke(logRecord, new object[] { scopeProvider });
            }
        }
        else
        {
            logRecord.FormattedMessage = "Non-structured log message";
        }

        if (index % 3 == 0)
        {
            logRecord.TraceId = ActivityTraceId.CreateRandom();
            logRecord.SpanId = ActivitySpanId.CreateRandom();

            if (index % 6 == 0)
            {
                logRecord.TraceFlags = ActivityTraceFlags.None;
            }
            else
            {
                logRecord.TraceFlags = ActivityTraceFlags.Recorded;
            }
        }

        if (index % 9 == 0)
        {
            logRecord.Exception = new InvalidOperationException();
        }

        return logRecord;
    }

    private sealed class NoopHttpClient : IHttpClient
    {
        public HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
            };
        }
    }

    private sealed class ScopeProvider : IExternalScopeProvider
    {
        private readonly List<KeyValuePair<string, object?>>[] scopes;

        public ScopeProvider(params List<KeyValuePair<string, object?>>[] scopes)
        {
            this.scopes = scopes;
        }

        public void ForEachScope<TState>(Action<object, TState> callback, TState state)
        {
            foreach (var scope in this.scopes)
            {
                callback(scope, state);
            }
        }

        public IDisposable Push(object state)
        {
            throw new NotImplementedException();
        }
    }
}
