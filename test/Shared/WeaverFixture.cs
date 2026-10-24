// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public sealed class WeaverFixture : XunitContainerFixture<IContainer>
{
    private const int MaxRegistryDownloadAttempts = 3;

    private readonly string inputFilePath = Path.GetTempFileName();
    private readonly SemaphoreSlim registryLock = new(1, 1);
    private readonly HashSet<string> downloadedRegistries = [];

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
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            File.SetUnixFileMode(this.inputFilePath, mode);
        }
#else
        File.WriteAllText(this.inputFilePath, telemetryJson);
#endif

        var registryPath = await this.GetRegistryAsync(version, cancellationToken).ConfigureAwait(false);

        // The gh_workflow_command diagnostic format does not render errors that are not policy
        // violations (such as failing to load the registry or to parse the input), so use the
        // default ansi format so that the cause of a non-zero exit code is visible in the output.
        string[] command =
        [
            "sh",
            "-c",
            "set -eu; " +
            $"/weaver/weaver registry live-check --registry {registryPath} --format json --input-source /weaver.json --input-format json --no-stream --diagnostic-format ansi --no-stats",
        ];

        return await this.Container
            .ExecAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        this.registryLock.Dispose();

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

    private async Task<string> GetRegistryAsync(Version version, CancellationToken cancellationToken)
    {
        var tag = version.ToString(3);
        var path = $"/tmp/semconv/{tag}";
        var registryPath = $"{path}/model";

        await this.registryLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (this.downloadedRegistries.Contains(tag))
            {
                return registryPath;
            }

            // Download the registry once per container instead of letting Weaver clone it from
            // GitHub for every check, so that a transient network failure does not fail a test.
            var archiveUrl = $"https://github.com/open-telemetry/semantic-conventions/archive/refs/tags/v{tag}.tar.gz";

            string[] command =
            [
                "sh",
                "-c",
                "set -euo pipefail; " +
                $"rm -rf {path}; " +
                $"mkdir -p {path}; " +
                $"wget -q -T 30 -O - {archiveUrl} | tar -xz -C {path} --strip-components=1 semantic-conventions-{tag}/model",
            ];

            for (var attempt = 1; ; attempt++)
            {
                var result = await this.Container
                    .ExecAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    this.downloadedRegistries.Add(tag);
                    return registryPath;
                }

                if (attempt >= MaxRegistryDownloadAttempts)
                {
                    throw new InvalidOperationException(
                        $"Failed to download Semantic Conventions {tag} registry from {archiveUrl} after {attempt} attempts. Exit code: {result.ExitCode}. {result.Stderr}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            this.registryLock.Release();
        }
    }
}
