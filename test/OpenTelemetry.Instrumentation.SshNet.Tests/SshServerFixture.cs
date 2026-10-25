// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using OpenTelemetry.Tests;
using Renci.SshNet;

namespace OpenTelemetry.Instrumentation.SshNet.Tests;

/// <summary>
/// Starts an OpenSSH server (with the SFTP subsystem) in Docker.
/// </summary>
public sealed class SshServerFixture : XunitContainerFixture<IContainer>
{
    private const string UserName = "otel";
    private const string Password = "otel-password";
    private const int SshPort = 2222;

    public string Host => this.TypedContainer.Hostname;

    public int Port => this.TypedContainer.GetMappedPublicPort(SshPort);

    protected override string DockerfileName => "sshd.Dockerfile";

    public ConnectionInfo CreateConnectionInfo(string? password = null) =>
        new PasswordConnectionInfo(this.Host, this.Port, UserName, password ?? Password)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

    public override async ValueTask InitializeAsync()
    {
        if (!DockerHelper.IsAvailable(DockerPlatform.Linux))
        {
            return;
        }

        await this.StartAsync();
        await this.WaitForSshAsync();
    }

    protected override IContainer CreateContainer() =>
        new ContainerBuilder(this.GetImage())
            .WithEnvironment("USER_NAME", UserName)
            .WithEnvironment("USER_PASSWORD", Password)
            .WithEnvironment("PASSWORD_ACCESS", "true")
            .WithPortBinding(SshPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(SshPort))
            .Build();

    // The port opens before sshd has applied the user/password config, so
    // retry a real login until it succeeds. Uses an uninstrumented client.
    private async Task WaitForSshAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            try
            {
                using var client = new SftpClient(this.CreateConnectionInfo());
                await client.ConnectAsync(CancellationToken.None);
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }
    }
}
