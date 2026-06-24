// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Data;
using OpenTelemetry.Tests;
using Testcontainers.Kusto;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public sealed class KustoIntegrationTestsFixture : ContainerFixture<KustoContainer>, IAsyncLifetime
{
    private readonly IDisposable queryBodyTracing;

    public KustoIntegrationTestsFixture()
    {
        // The instrumentation deliberately does not set this; query-body tracing is enabled here (by the host)
        // so the Kusto client emits the query text the instrumentation parses. The client reads it once, so
        // set it before any client is created and restore it when the fixture is disposed.
        this.queryBodyTracing = EnvironmentVariableScope.Create("KUSTO_DATA_TRACE_REQUEST_BODY", "1");
    }

    public KustoContainer DatabaseContainer => this.TypedContainer;

    public KustoConnectionStringBuilder ConnectionStringBuilder => new(this.DatabaseContainer.GetConnectionString());

    protected override string DockerfileName => "kusto.Dockerfile";

    async Task IAsyncLifetime.DisposeAsync()
    {
        await this.DisposeAsync();
        this.queryBodyTracing.Dispose();
    }

    protected override KustoContainer CreateContainer() => new KustoBuilder(this.GetImage()).Build();
}
