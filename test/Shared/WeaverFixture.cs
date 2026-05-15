// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public sealed class WeaverFixture : XunitContainerFixture<IContainer>
{
    private readonly string inputFilePath = Path.GetTempFileName();

    protected override string DockerfileName => "weaver.Dockerfile";

    public async Task<ExecResult> CheckAsync(
        string telemetryJson,
        Version version,
        CancellationToken cancellationToken = default)
    {
#if NET
        await File.WriteAllTextAsync(this.inputFilePath, telemetryJson, cancellationToken);

        if (OperatingSystem.IsLinux())
        {
            var mode = UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            File.SetUnixFileMode(this.inputFilePath, mode);
        }
#else
        File.WriteAllText(this.inputFilePath, telemetryJson);
#endif

        var registryUrl = $"https://github.com/open-telemetry/semantic-conventions.git@v{version.ToString(3)}[model]";

        string[] command =
        [
            "sh",
            "-c",
            "set -eu; " +
            $"/weaver/weaver registry live-check --registry {registryUrl} --format json --input-source /weaver.json --input-format json --no-stream --diagnostic-format gh_workflow_command --no-stats",
        ];

        return await this.Container
            .ExecAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        try
        {
            if (File.Exists(this.inputFilePath))
            {
                File.Delete(this.inputFilePath);
            }
        }
        catch (Exception)
        {
            // Ignore
        }

        GC.SuppressFinalize(this);
    }

    protected override IContainer CreateContainer() =>
        new ContainerBuilder(this.GetImage())
            .WithEntrypoint("sh", "-c")
            .WithCommand("sleep infinity")
            .WithBindMount(this.inputFilePath, "/weaver.json")
            .Build();
}
