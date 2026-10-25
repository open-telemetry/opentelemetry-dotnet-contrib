// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace OpenTelemetry.Instrumentation.SshNet.Tests;

[Collection(SshNetTestCollection.Name)]
public sealed class SshNetIntegrationTests : IClassFixture<SshServerFixture>, IDisposable
{
    private readonly SshServerFixture server;
    private readonly List<Activity> spans = [];
    private readonly List<Metric> metrics = [];
    private readonly TracerProvider tracerProvider;
    private readonly MeterProvider meterProvider;

    public SshNetIntegrationTests(SshServerFixture server)
    {
        this.server = server;

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSshNetInstrumentation()
            .AddInMemoryExporter(this.spans)
            .Build();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSshNetInstrumentation()
            .AddInMemoryExporter(this.metrics)
            .Build();
    }

    public void Dispose()
    {
        this.tracerProvider.Dispose();
        this.meterProvider.Dispose();
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task SftpRoundTrip_ProducesSpansForEachOperation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = $"/tmp/otel-{Guid.NewGuid():N}";
        var path = $"{directory}/hello.txt";
        var content = "hello from opentelemetry"u8.ToArray();

        using (var client = new SftpClient(this.server.CreateConnectionInfo()).WithOpenTelemetry())
        {
            await client.ConnectAsync(cancellationToken);
            await client.CreateDirectoryAsync(directory, cancellationToken);

            using (var input = new MemoryStream(content))
            {
                await client.UploadFileAsync(input, path, cancellationToken);
            }

            Assert.True(await client.ExistsAsync(path, cancellationToken));

            var names = new List<string>();
            await foreach (var file in client.ListDirectoryAsync(directory, cancellationToken))
            {
                names.Add(file.Name);
            }

            Assert.Contains("hello.txt", names);

            using (var output = new MemoryStream())
            {
                await client.DownloadFileAsync(path, output, cancellationToken);
                Assert.Equal(content, output.ToArray());
            }

            await client.DeleteFileAsync(path, cancellationToken);
            await client.DeleteDirectoryAsync(directory, cancellationToken);
        }

        var flushSpans = this.FlushSpans();
        Assert.Equal(
            [
                "sftp Connect",
                "sftp CreateDirectory",
                "sftp UploadFile",
                "sftp Exists",
                "sftp ListDirectory",
                "sftp DownloadFile",
                "sftp DeleteFile",
                "sftp DeleteDirectory",
            ],
            flushSpans.Select(s => s.DisplayName));

        Assert.All(flushSpans, s =>
        {
            Assert.Equal(ActivityKind.Client, s.Kind);
            Assert.Equal(ActivityStatusCode.Unset, s.Status);
            Assert.Equal(this.server.Host, s.GetTagItem("server.address"));
            Assert.Equal(this.server.Port, s.GetTagItem("server.port"));
        });

        var upload = flushSpans.Single(s => s.DisplayName == "sftp UploadFile");
        Assert.Equal(path, upload.GetTagItem("file.path"));
        Assert.Equal((long)content.Length, upload.GetTagItem("sftp.transfer.bytes"));

        var download = flushSpans.Single(s => s.DisplayName == "sftp DownloadFile");
        Assert.Equal((long)content.Length, download.GetTagItem("sftp.transfer.bytes"));

        this.meterProvider.ForceFlush();
        var duration = Assert.Single(this.metrics, m => m.Name == "ssh.client.operation.duration");
        long count = 0;
        foreach (ref readonly var point in duration.GetMetricPoints())
        {
            count += point.GetHistogramCount();
        }

        Assert.Equal(flushSpans.Count, count);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void DownloadMissingFile_RecordsError()
    {
        using var client = new SftpClient(this.server.CreateConnectionInfo()).WithOpenTelemetry();
        client.Connect();

        using var output = new MemoryStream();
        Assert.Throws<SftpPathNotFoundException>(() => client.DownloadFile($"/tmp/missing-{Guid.NewGuid():N}", output));

        var span = this.FlushSpans().Single(s => s.DisplayName == "sftp DownloadFile");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(typeof(SftpPathNotFoundException).FullName, span.GetTagItem("error.type"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ConnectWithWrongPassword_RecordsError()
    {
        using var client = new SftpClient(this.server.CreateConnectionInfo("wrong-password")).WithOpenTelemetry();

        await Assert.ThrowsAsync<SshAuthenticationException>(() => client.ConnectAsync(TestContext.Current.CancellationToken));

        var span = Assert.Single(this.FlushSpans());
        Assert.Equal("sftp Connect", span.DisplayName);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(typeof(SshAuthenticationException).FullName, span.GetTagItem("error.type"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void SshRunCommand_RecordsExitCodeAndCommandText()
    {
        using var client = new SshClient(this.server.CreateConnectionInfo())
            .WithOpenTelemetry(options => options.RecordCommandText = true);
        client.Connect();

        var command = client.RunCommand("echo hello; exit 3");
        Assert.Equal("hello\n", command.Result);

        var flushSpans = this.FlushSpans();
        Assert.Equal(["ssh Connect", "ssh RunCommand"], flushSpans.Select(s => s.DisplayName));

        var run = flushSpans[1];
        Assert.Equal("ssh", run.GetTagItem("network.protocol.name"));
        Assert.Equal("echo hello; exit 3", run.GetTagItem("ssh.command.text"));
        Assert.Equal(3, run.GetTagItem("ssh.command.exit_code"));
    }

    private List<Activity> FlushSpans()
    {
        this.tracerProvider.ForceFlush();
        return this.spans;
    }
}
