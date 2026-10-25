// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace OpenTelemetry.Instrumentation.SshNet.Tests;

[Collection(SshNetTestCollection.Name)]
public sealed class SshNetInstrumentationTests : IDisposable
{
    private readonly List<Activity> spans = [];
    private readonly List<Metric> metrics = [];
    private readonly TracerProvider tracerProvider;
    private readonly MeterProvider meterProvider;
    private readonly InterfaceFake<ISftpClient> sftp;
    private readonly InterfaceFake<ISshClient> ssh;

    public SshNetInstrumentationTests()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSshNetInstrumentation()
            .AddInMemoryExporter(this.spans)
            .Build();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSshNetInstrumentation()
            .AddInMemoryExporter(this.metrics)
            .Build();

        var connectionInfo = new PasswordConnectionInfo("sftp.example.com", 2222, "user", "password");

        this.sftp = InterfaceFake.Create<ISftpClient>()
            .On("get_ConnectionInfo", _ => connectionInfo);

        this.ssh = InterfaceFake.Create<ISshClient>()
            .On("get_ConnectionInfo", _ => connectionInfo);
    }

    public void Dispose()
    {
        this.tracerProvider.Dispose();
        this.meterProvider.Dispose();
    }

    [Fact]
    public void UploadFile_CreatesClientSpanWithAttributes()
    {
        this.sftp.On(nameof(ISftpClient.UploadFile), args =>
        {
            ((Stream)args[0]!).Position = 42;
            return null;
        });

        using var input = new MemoryStream(new byte[42]);
        this.sftp.Instance.WithOpenTelemetry().UploadFile(input, "/upload/a.txt");

        var call = Assert.Single(this.sftp.CallsTo(nameof(ISftpClient.UploadFile)));
        Assert.Same(input, call[0]);
        Assert.Equal("/upload/a.txt", call[1]);

        var span = this.SingleSpan();
        Assert.Equal("sftp UploadFile", span.DisplayName);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Equal("sftp", span.GetTagItem("network.protocol.name"));
        Assert.Equal("UploadFile", span.GetTagItem("ssh.operation.name"));
        Assert.Equal("sftp.example.com", span.GetTagItem("server.address"));
        Assert.Equal(2222, span.GetTagItem("server.port"));
        Assert.Equal("/upload/a.txt", span.GetTagItem("file.path"));
        Assert.Equal(42L, span.GetTagItem("sftp.transfer.bytes"));
    }

    [Fact]
    public async Task DownloadFileAsync_RecordsBytesWritten()
    {
        this.sftp.On(nameof(ISftpClient.DownloadFileAsync), args =>
        {
            ((Stream)args[1]!).Write(new byte[10], 0, 10);
            return Task.CompletedTask;
        });

        using var output = new MemoryStream();
        await this.sftp.Instance.WithOpenTelemetry().DownloadFileAsync("/data/b.bin", output, TestContext.Current.CancellationToken);

        var span = this.SingleSpan();
        Assert.Equal("sftp DownloadFile", span.DisplayName);
        Assert.Equal(10L, span.GetTagItem("sftp.transfer.bytes"));
    }

    [Fact]
    public void RenameFile_RecordsSourceAndTargetPaths()
    {
        this.sftp.Instance.WithOpenTelemetry().RenameFile("/a.txt", "/b.txt");

        var span = this.SingleSpan();
        Assert.Equal("/a.txt", span.GetTagItem("file.path"));
        Assert.Equal("/b.txt", span.GetTagItem("sftp.target.path"));
    }

    [Fact]
    public void RecordFilePathFalse_OmitsPaths()
    {
        var client = this.sftp.Instance.WithOpenTelemetry(options => options.RecordFilePath = false);
        client.RenameFile("/a.txt", "/b.txt");

        var span = this.SingleSpan();
        Assert.Null(span.GetTagItem("file.path"));
        Assert.Null(span.GetTagItem("sftp.target.path"));
    }

    [Fact]
    public void Failure_SetsErrorStatusAndRethrows()
    {
        var error = new SftpPathNotFoundException("No such file");
        this.sftp.On(nameof(ISftpClient.ReadAllText), _ => throw error);

        var client = this.sftp.Instance.WithOpenTelemetry(options => options.RecordException = true);
        var thrown = Assert.Throws<SftpPathNotFoundException>(() => client.ReadAllText("/missing.txt"));
        Assert.Same(error, thrown);

        var span = this.SingleSpan();
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("No such file", span.StatusDescription);
        Assert.Equal(typeof(SftpPathNotFoundException).FullName, span.GetTagItem("error.type"));
        Assert.Contains(span.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task FailureAsync_SetsErrorStatus()
    {
        this.sftp.On(
            nameof(ISftpClient.ExistsAsync),
            _ => Task.FromException<bool>(new SshConnectionException("Connection lost")));

        var client = this.sftp.Instance.WithOpenTelemetry();
        await Assert.ThrowsAsync<SshConnectionException>(() => client.ExistsAsync("/x", TestContext.Current.CancellationToken));

        var span = this.SingleSpan();
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(typeof(SshConnectionException).FullName, span.GetTagItem("error.type"));
        Assert.DoesNotContain(span.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task ListDirectoryAsync_SpanCoversEnumeration()
    {
        var files = new[] { InterfaceFake.Create<ISftpFile>().Instance, InterfaceFake.Create<ISftpFile>().Instance };
        this.sftp.On(nameof(ISftpClient.ListDirectoryAsync), _ => ToAsync(files));

        var results = new List<ISftpFile>();
        await foreach (var file in this.sftp.Instance.WithOpenTelemetry().ListDirectoryAsync("/dir", TestContext.Current.CancellationToken))
        {
            Assert.Empty(this.spans);
            results.Add(file);
        }

        Assert.Equal(files, results);
        var span = this.SingleSpan();
        Assert.Equal("sftp ListDirectory", span.DisplayName);
        Assert.Equal("/dir", span.GetTagItem("file.path"));
    }

    [Fact]
    public void Span_IsChildOfAmbientActivity()
    {
        using var parentSource = new ActivitySource("test-parent");
        using var listener = new ActivityListener();

        listener.ShouldListenTo = s => s.Name == "test-parent";
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        using var parent = parentSource.StartActivity();
        this.sftp.Instance.WithOpenTelemetry().Exists("/x");

        var span = this.SingleSpan();
        Assert.Equal(parent!.TraceId, span.TraceId);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
    }

    [Fact]
    public void Connect_IsTraced()
    {
        this.sftp.Instance.WithOpenTelemetry().Connect();

        Assert.Single(this.sftp.CallsTo(nameof(ISftpClient.Connect)));
        Assert.Equal("sftp Connect", this.SingleSpan().DisplayName);
    }

    [Fact]
    public void SshRunCommand_RecordsCommandTextOnlyWhenOptedIn()
    {
        this.ssh.On(nameof(ISshClient.RunCommand), _ => throw new SshException("boom"));

        Assert.Throws<SshException>(() => this.ssh.Instance.WithOpenTelemetry().RunCommand("ls -la"));
        Assert.Throws<SshException>(() => this.ssh.Instance
            .WithOpenTelemetry(options => options.RecordCommandText = true)
            .RunCommand("ls -la"));

        this.tracerProvider.ForceFlush();
        Assert.Equal(2, this.spans.Count);
        Assert.All(this.spans, s => Assert.Equal("ssh RunCommand", s.DisplayName));
        Assert.Null(this.spans[0].GetTagItem("ssh.command.text"));
        Assert.Equal("ls -la", this.spans[1].GetTagItem("ssh.command.text"));
        Assert.Equal(ActivityStatusCode.Error, this.spans[1].Status);
    }

    [Fact]
    public void OperationDuration_IsRecorded()
    {
        this.sftp.On(nameof(ISftpClient.Delete), _ => throw new SftpPermissionDeniedException("denied"));

        var client = this.sftp.Instance.WithOpenTelemetry();
        client.Exists("/x");
        Assert.Throws<SftpPermissionDeniedException>(() => client.Delete("/y"));

        this.meterProvider.ForceFlush();
        var metric = Assert.Single(this.metrics, m => m.Name == "ssh.client.operation.duration");
        Assert.Equal("s", metric.Unit);
        Assert.Equal(MetricType.Histogram, metric.MetricType);

        var points = new List<MetricPoint>();
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            points.Add(point);
        }

        Assert.Equal(2, points.Count);

        var tagsByOperation = points.ToDictionary(
            p => (string)Tags(p)["ssh.operation.name"]!,
            Tags);

        Assert.Equal("sftp.example.com", tagsByOperation["Exists"]["server.address"]);
        Assert.False(tagsByOperation["Exists"].ContainsKey("error.type"));
        Assert.False(tagsByOperation["Exists"].ContainsKey("file.path"));
        Assert.Equal(typeof(SftpPermissionDeniedException).FullName, tagsByOperation["Delete"]["error.type"]);
    }

    [Fact]
    public void WithOpenTelemetry_DoesNotDoubleWrap()
    {
        var once = this.sftp.Instance.WithOpenTelemetry();
        Assert.Same(once, once.WithOpenTelemetry());

        once.Exists("/x");
        this.SingleSpan();
    }

    [Fact]
    public void WithoutListeners_OperationsStillWork()
    {
        this.tracerProvider.Dispose();
        this.meterProvider.Dispose();

        this.sftp.On(nameof(ISftpClient.Exists), _ => true);
        Assert.True(this.sftp.Instance.WithOpenTelemetry().Exists("/x"));
    }

    private static async IAsyncEnumerable<ISftpFile> ToAsync(IEnumerable<ISftpFile> files)
    {
        foreach (var file in files)
        {
            await Task.Yield();
            yield return file;
        }
    }

    private static Dictionary<string, object?> Tags(MetricPoint point)
    {
        var tags = new Dictionary<string, object?>();
        foreach (var tag in point.Tags)
        {
            tags[tag.Key] = tag.Value;
        }

        return tags;
    }

    private Activity SingleSpan()
    {
        this.tracerProvider.ForceFlush();
        return Assert.Single(this.spans);
    }
}
