// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public abstract class ContainerFixture : IAsyncDisposable
{
    private bool started;

    protected abstract IContainer Container { get; }

    protected abstract string DockerfileName { get; }

    public virtual async ValueTask DisposeAsync()
    {
        if (this.started)
        {
            await this.Container.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    public async Task StartAsync()
    {
        if (this.started)
        {
            return;
        }

        const int MaxAttempts = 3;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await this.Container.StartAsync();
                break;
            }
            catch when (attempt < MaxAttempts)
            {
                // Some container images (e.g. SQL Server on Linux) can crash on startup
                // under CI resource pressure; restarting is the common workaround for
                // this failure mode. See https://github.com/microsoft/aspire/issues/5055.
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        this.started = true;
    }

    public Uri GetBaseAddress(int port) =>
        new UriBuilder(Uri.UriSchemeHttp, this.Container.Hostname, this.Container.GetMappedPublicPort(port)).Uri;

    protected string GetImage()
    {
        var assembly = this.GetType().Assembly;

        using var stream = assembly.GetManifestResourceStream(this.DockerfileName);

#if NET
        using var reader = new StreamReader(stream!);
#else
        using var reader = new StreamReader(stream);
#endif

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
