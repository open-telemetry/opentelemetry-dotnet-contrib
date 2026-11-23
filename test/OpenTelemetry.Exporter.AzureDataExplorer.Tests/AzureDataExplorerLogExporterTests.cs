// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class AzureDataExplorerLogExporterTests
{
    [Fact]
    public void ExportsLogsUsingCollectorSchema()
    {
        var ingestor = new TestIngestor();
        ActivityTraceId traceId;
        ActivitySpanId spanId;

        using (var serviceProvider = CreateServiceProvider(ingestor, o => o.LogsTableJsonMapping = "log_mapping"))
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MyCategory");

            using var activity = new Activity("test").Start();
            traceId = activity.TraceId;
            spanId = activity.SpanId;

            using (logger.BeginScope(new Dictionary<string, object> { ["scope.key"] = "scope-value" }))
            {
                logger.LogWarning(new EventId(42, "MyEvent"), "Hello {Name}", "World");
            }

            logger.LogError(new InvalidOperationException("bad"), "Failed");

            serviceProvider.GetRequiredService<LoggerProvider>().ForceFlush();
        }

        Assert.Equal("OTELLogs", ingestor.TableName);
        Assert.Equal("log_mapping", ingestor.Mapping);
        Assert.Equal(1, ingestor.DisposeCount);

        var rows = ingestor.Rows.ToList();
        Assert.Equal(2, rows.Count);

        var warning = rows[0];
        Assert.Equal("Warn", warning.GetProperty("SeverityText").GetString());
        Assert.Equal(13, warning.GetProperty("SeverityNumber").GetInt32());
        Assert.Equal("Hello World", warning.GetProperty("Body").GetString());
        Assert.Equal(traceId.ToHexString(), warning.GetProperty("TraceID").GetString());
        Assert.Equal(spanId.ToHexString(), warning.GetProperty("SpanID").GetString());
        Assert.Equal(warning.GetProperty("Timestamp").GetString(), warning.GetProperty("ObservedTimestamp").GetString());
        Assert.Equal("my-service", warning.GetProperty("ResourceAttributes").GetProperty("service.name").GetString());

        var attributes = warning.GetProperty("LogsAttributes");
        Assert.Equal("World", attributes.GetProperty("Name").GetString());
        Assert.Equal("Hello {Name}", attributes.GetProperty("{OriginalFormat}").GetString());
        Assert.Equal(42, attributes.GetProperty("event.id").GetInt32());
        Assert.Equal("MyEvent", attributes.GetProperty("event.name").GetString());
        Assert.Equal("scope-value", attributes.GetProperty("scope.key").GetString());
        Assert.Equal("MyCategory", attributes.GetProperty("scope.name").GetString());

        var error = rows[1];
        Assert.Equal("Error", error.GetProperty("SeverityText").GetString());
        Assert.Equal(17, error.GetProperty("SeverityNumber").GetInt32());
        Assert.Equal("Failed", error.GetProperty("Body").GetString());
        var errorAttributes = error.GetProperty("LogsAttributes");
        Assert.Equal(typeof(InvalidOperationException).FullName, errorAttributes.GetProperty("exception.type").GetString());
        Assert.Equal("bad", errorAttributes.GetProperty("exception.message").GetString());
        Assert.Contains("bad", errorAttributes.GetProperty("exception.stacktrace").GetString(), StringComparison.Ordinal);
        Assert.False(errorAttributes.TryGetProperty("scope.key", out _));
    }

    [Theory]
    [InlineData(LogLevel.Trace, "Trace", 1)]
    [InlineData(LogLevel.Debug, "Debug", 5)]
    [InlineData(LogLevel.Information, "Info", 9)]
    [InlineData(LogLevel.Warning, "Warn", 13)]
    [InlineData(LogLevel.Error, "Error", 17)]
    [InlineData(LogLevel.Critical, "Fatal", 21)]
    [InlineData(LogLevel.None, "", 0)]
    public void SeverityIsDerivedFromLogLevel(LogLevel logLevel, string expectedText, int expectedNumber)
    {
        var (text, number) = AdxLogSerializer.GetSeverity(logLevel);

        Assert.Equal(expectedText, text);
        Assert.Equal(expectedNumber, number);
    }

    private static ServiceProvider CreateServiceProvider(TestIngestor ingestor, Action<AzureDataExplorerExporterOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.Clear().AddService("my-service"))
            .WithLogging(
                builder => builder.AddAzureDataExplorerExporter(o =>
                {
                    TestIngestor.Configure(o, ingestor);
                    configure(o);
                }),
                options =>
                {
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                });

        return services.BuildServiceProvider();
    }
}
