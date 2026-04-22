// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Cassandra;
using Cassandra.Mapping;
using Cassandra.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;
using CassandraData = Cassandra.Data.Linq;

namespace OpenTelemetry.Instrumentation.Cassandra.Tests;

[Collection(CassandraCollection.Name)]
[Trait("CategoryName", "CassandraIntegrationTests")]
public class CassandraInstrumentationTests(CassandraFixture fixture)
{
    private const int MaxTimeToAllowForFlush = 20000;

    private readonly string cassandraConnectionString = fixture.Container.GetConnectionString() + ";Default Keyspace=OT_Cassandra_Testing";

    [Fact]
    public void AddCassandraInstrumentationDoesNotThrow()
    {
        var builder = Sdk.CreateMeterProviderBuilder();

        var actual = builder.AddCassandraInstrumentation();

        Assert.Same(builder, actual);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task CassandraMetricsAreCaptured()
    {
        var exportedItems = new List<Metric>();

        IEnumerable<BooksEntity> books;

        using (var provider = Sdk.CreateMeterProviderBuilder()
                                 .AddInMemoryExporter(exportedItems)
                                 .AddCassandraInstrumentation()
                                 .Build())
        {
            var cluster = new Builder()
            .WithConnectionString(this.cassandraConnectionString)
            .WithOpenTelemetryMetrics()
            .Build();

            var session = cluster.ConnectAndCreateDefaultKeyspaceIfNotExists();

            var table = new CassandraData.Table<BooksEntity>(session, new MappingConfiguration());

            await table.CreateIfNotExistsAsync();

            var mapper = new Mapper(session);

            await mapper.InsertAsync(new BooksEntity(Guid.NewGuid(), "Good book"));
            await mapper.InsertAsync(new BooksEntity(Guid.NewGuid(), "Bad book"));

            books = await mapper.FetchAsync<BooksEntity>();

            provider.ForceFlush(MaxTimeToAllowForFlush);
        }

        Assert.True(exportedItems.Count > 1, $"Expected more than 1 metric, but found {exportedItems.Count}");
        Assert.NotEmpty(books);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task CassandraMetricsWithCustomOptionsCaptured()
    {
        var exportedItems = new List<Metric>();

        IEnumerable<BooksEntity> books;

        using (var provider = Sdk.CreateMeterProviderBuilder()
                                 .AddInMemoryExporter(exportedItems)
                                 .AddCassandraInstrumentation()
                                 .Build())
        {
            var options = new DriverMetricsOptions();

            options.SetEnabledNodeMetrics([NodeMetric.Gauges.InFlight]);
            options.SetEnabledSessionMetrics([SessionMetric.Gauges.ConnectedNodes]);

            var cluster = new Builder()
                .WithConnectionString(this.cassandraConnectionString)
                .WithOpenTelemetryMetrics(options)
                .Build();

            var session = cluster.ConnectAndCreateDefaultKeyspaceIfNotExists();

            var table = new CassandraData.Table<BooksEntity>(session, new MappingConfiguration());

            await table.CreateIfNotExistsAsync();

            var mapper = new Mapper(session);

            await mapper.InsertAsync(new BooksEntity(Guid.NewGuid(), "Good book"));
            await mapper.InsertAsync(new BooksEntity(Guid.NewGuid(), "Bad book"));

            books = await mapper.FetchAsync<BooksEntity>();

            provider.ForceFlush(MaxTimeToAllowForFlush);
        }

        Assert.NotEmpty(books);

        var inFlightConnection = exportedItems.FirstOrDefault(i => i.Name == "cassandra.pool.in-flight");
        Assert.NotNull(inFlightConnection);

        var connected = exportedItems.FirstOrDefault(i => i.Name == "cassandra.connected-nodes");
        Assert.NotNull(connected);

        var metricNames = exportedItems.Select(i => i.Name).Distinct();
        Assert.Equal(2, metricNames.Count());
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task CassandraRequestsLatencyMetricsAreCaptured()
    {
        var exportedItems = new List<Metric>();

        IEnumerable<BooksEntity> books;

        using (var provider = Sdk.CreateMeterProviderBuilder()
                                 .AddInMemoryExporter(exportedItems)
                                 .AddCassandraInstrumentation()
                                 .Build())
        {
            var cluster = new Builder()
                .WithConnectionString(this.cassandraConnectionString)
                .WithOpenTelemetryMetrics()
                .Build();

            var session = cluster.ConnectAndCreateDefaultKeyspaceIfNotExists();

            var table = new CassandraData.Table<BooksEntity>(session, new MappingConfiguration());

            await table.CreateIfNotExistsAsync();

            var mapper = new Mapper(session);

            await mapper.InsertAsync(new BooksEntity(Guid.NewGuid(), "Good book"));
            await mapper.InsertAsync(new BooksEntity(Guid.NewGuid(), "Bad book"));

            books = await mapper.FetchAsync<BooksEntity>();

            provider.ForceFlush(MaxTimeToAllowForFlush);
        }

        var cqlMessageLatency = exportedItems.FirstOrDefault(i => i.Name == "cassandra.cql-requests");
        Assert.NotNull(cqlMessageLatency);
        Assert.NotEmpty(books);
    }
}
