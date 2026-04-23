// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public abstract class ContainerFixture : IAsyncDisposable
{
    protected abstract IContainer Container { get; }

    protected abstract string DockerfileName { get; }

    public async ValueTask DisposeAsync()
    {
        await this.Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public Task StartAsync() => this.Container.StartAsync();

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
