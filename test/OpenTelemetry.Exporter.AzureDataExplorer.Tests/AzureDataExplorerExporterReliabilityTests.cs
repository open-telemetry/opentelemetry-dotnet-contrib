// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class AzureDataExplorerExporterReliabilityTests
{
    [Fact]
    public void TransientFailuresAreRetried()
    {
        var ingestor = new TestIngestor();
        ingestor.FailWith(new IOException("transient"), new TimeoutException("transient"));
        using var exporter = CreateExporter(ingestor, o => o.RetryInitialInterval = TimeSpan.FromMilliseconds(1));

        Assert.Equal(ExportResult.Success, Export(exporter));
        Assert.Equal(3, ingestor.Attempts);
        Assert.Single(ingestor.Rows);
    }

    [Fact]
    public void PermanentFailuresAreNotRetried()
    {
        var ingestor = new TestIngestor();
        ingestor.FailWith(new AzureDataExplorerIngestionException("bad mapping", isPermanent: true));
        using var exporter = CreateExporter(ingestor, o => o.RetryInitialInterval = TimeSpan.FromMilliseconds(1));

        Assert.Equal(ExportResult.Failure, Export(exporter));
        Assert.Equal(1, ingestor.Attempts);
    }

    [Fact]
    public void RetriesCanBeDisabled()
    {
        var ingestor = new TestIngestor();
        ingestor.FailWith(new IOException("transient"));
        using var exporter = CreateExporter(ingestor, o => o.RetryOnFailure = false);

        Assert.Equal(ExportResult.Failure, Export(exporter));
        Assert.Equal(1, ingestor.Attempts);
    }

    [Fact]
    public void RetriesStopAtExportTimeout()
    {
        var ingestor = new TestIngestor();
        ingestor.FailWith(Enumerable.Range(0, 100).Select(_ => new IOException("transient")).ToArray());
        using var exporter = CreateExporter(ingestor, o =>
        {
            o.ExportTimeout = TimeSpan.FromMilliseconds(500);
            o.RetryInitialInterval = TimeSpan.FromMilliseconds(50);
            o.RetryMaxInterval = TimeSpan.FromMilliseconds(100);
        });

        var stopwatch = Stopwatch.StartNew();
        Assert.Equal(ExportResult.Failure, Export(exporter));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.InRange(ingestor.Attempts, 2, 20);
    }

    [Fact]
    public void IngestionIsCancelledAtExportTimeout()
    {
        var ingestor = new TestIngestor { OnIngest = ct => Task.Delay(Timeout.Infinite, ct) };
        using var exporter = CreateExporter(ingestor, o =>
        {
            o.ExportTimeout = TimeSpan.FromMilliseconds(200);
            o.RetryOnFailure = false;
        });

        Assert.Equal(ExportResult.Failure, Export(exporter));
    }

    [Fact]
    public async Task ShutdownInterruptsRetryWait()
    {
        var ingestor = new TestIngestor();
        ingestor.FailWith(new IOException("transient"));
        using var exporter = CreateExporter(ingestor, o => o.RetryInitialInterval = TimeSpan.FromSeconds(20));

        var export = Task.Run(() => Export(exporter), TestContext.Current.CancellationToken);
        SpinWait.SpinUntil(() => ingestor.Attempts > 0, TimeSpan.FromSeconds(5));
        Thread.Sleep(50);
        var stopwatch = Stopwatch.StartNew();
        exporter.Shutdown();

        Assert.Equal(ExportResult.Failure, await export);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal(1, ingestor.DisposeCount);
    }

    [Fact]
    public async Task ShutdownCancelsInFlightIngestion()
    {
        var ingestor = new TestIngestor { OnIngest = ct => Task.Delay(Timeout.Infinite, ct) };
        using var exporter = CreateExporter(ingestor, o => o.ExportTimeout = TimeSpan.FromMinutes(5));

        var export = Task.Run(() => Export(exporter), TestContext.Current.CancellationToken);
        SpinWait.SpinUntil(() => ingestor.Attempts > 0, TimeSpan.FromSeconds(5));
        var stopwatch = Stopwatch.StartNew();
        exporter.Shutdown();

        Assert.Equal(ExportResult.Failure, await export);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void IngestorIsDisposedOnce()
    {
        var ingestor = new TestIngestor();
        var exporter = CreateExporter(ingestor, _ => { });

        exporter.Shutdown();
        exporter.Dispose();

        Assert.Equal(1, ingestor.DisposeCount);
    }

    [Fact]
    public void EmptyBatchesAreNotIngested()
    {
        var ingestor = new TestIngestor();
        using var exporter = CreateExporter(ingestor, _ => { });

        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([], 0)));
        Assert.Equal(0, ingestor.Attempts);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 400)]
    [InlineData(4, 500)]
    [InlineData(40, 500)]
    public void RetryDelayUsesExponentialBackoff(int attempt, int expectedMilliseconds)
    {
        using var exporter = CreateExporter(new TestIngestor(), o =>
        {
            o.RetryInitialInterval = TimeSpan.FromMilliseconds(100);
            o.RetryMaxInterval = TimeSpan.FromMilliseconds(500);
        });

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), exporter.GetRetryDelay(attempt));
    }

    [Fact]
    public void IsPermanentClassifiesExceptions()
    {
        Assert.True(AzureDataExplorerTraceExporter.IsPermanent(new ArgumentException("bad")));
        Assert.True(AzureDataExplorerTraceExporter.IsPermanent(new AggregateException(new AzureDataExplorerIngestionException("bad", isPermanent: true))));
        Assert.True(AzureDataExplorerTraceExporter.IsPermanent(new AzureDataExplorerIngestionException("bad", isPermanent: true)));
        Assert.False(AzureDataExplorerTraceExporter.IsPermanent(new AzureDataExplorerIngestionException("throttled", isPermanent: false)));
        Assert.False(AzureDataExplorerTraceExporter.IsPermanent(new IOException("transient")));
        Assert.False(AzureDataExplorerTraceExporter.IsPermanent(new AggregateException(new ArgumentException("a"), new ArgumentException("b"))));
    }

    private static AzureDataExplorerTraceExporter CreateExporter(TestIngestor ingestor, Action<AzureDataExplorerExporterOptions> configure)
    {
        var options = TestIngestor.CreateOptions(ingestor);
        configure(options);
        return new AzureDataExplorerTraceExporter(options);
    }

    private static ExportResult Export(AzureDataExplorerTraceExporter exporter)
    {
        using var activity = new Activity("span");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        activity.Stop();
        return exporter.Export(new Batch<Activity>([activity], 1));
    }
}
