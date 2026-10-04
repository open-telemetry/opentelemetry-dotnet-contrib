// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class EntityFrameworkDiagnosticListenerTests : IDisposable
{
    private readonly DbContextOptions<ItemsContext> contextOptions;
    private readonly DbConnection connection;

    public EntityFrameworkDiagnosticListenerTests()
    {
        this.contextOptions = new DbContextOptionsBuilder<ItemsContext>()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;

        this.connection = RelationalOptionsExtension.Extract(this.contextOptions).Connection!;

        this.Seed();
    }

    public static TheoryData<string, string> DbSystemTestCases()
    {
        var testCases = new TheoryData<string, string>()
        {
            { "Microsoft.EntityFrameworkCore.Cosmos", "azure.cosmosdb" },
            { "MongoDB.EntityFrameworkCore", "mongodb" },
        };

        // Couchbase
        string[] names =
        [
            "Couchbase.EntityFrameworkCore",
            "Couchbase.EntityFrameworkCore.Storage.Internal",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "couchbase");
        }

        // DB2
        names =
        [
            "IBM.EntityFrameworkCore",
            "IBM.EntityFrameworkCore-lnx",
            "IBM.EntityFrameworkCore-osx",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "ibm.db2");
        }

        // Firebird
        names =
        [
            "FirebirdSql.Data.FirebirdClient.FbCommand",
            "FirebirdSql.EntityFrameworkCore.Firebird",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "firebirdsql");
        }

        // Microsoft SQL Server
        names =
        [
            "Microsoft.Data.SqlClient.SqlCommand",
            "Microsoft.EntityFrameworkCore.SqlServer",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "microsoft.sql_server");
        }

        // MySQL
        names =
        [
            "Devart.Data.MySql.Entity.EFCore",
            "Devart.Data.MySql.MySqlCommand",
            "MySql.Data.EntityFrameworkCore",
            "MySql.Data.MySqlClient.MySqlCommand",
            "MySql.EntityFrameworkCore",
            "Pomelo.EntityFrameworkCore.MySql",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "mysql");
        }

        // Oracle Database
        names =
        [
            "Devart.Data.Oracle.Entity.EFCore",
            "Devart.Data.Oracle.OracleCommand",
            "Oracle.EntityFrameworkCore",
            "Oracle.ManagedDataAccess.Client.OracleCommand",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "oracle.db");
        }

        // PostgreSQL
        names =
        [
            "Devart.Data.PostgreSql.Entity.EFCore",
            "Devart.Data.PostgreSql.PgSqlCommand",
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "Npgsql.NpgsqlCommand",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "postgresql");
        }

        // SQLite
        names =
        [
            "Devart.Data.SQLite.Entity.EFCore",
            "Microsoft.Data.Sqlite.SqliteCommand",
            "Microsoft.EntityFrameworkCore.Sqlite",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "sqlite");
        }

        // Spanner
        names =
        [
            "Google.Cloud.EntityFrameworkCore.Spanner",
            "Google.Cloud.Spanner.Data.SpannerCommand",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "gcp.spanner");
        }

        // Teradata
        names =
        [
            "Teradata.Client.Provider.TdCommand",
            "Teradata.EntityFrameworkCore",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "teradata");
        }

        // Unknown providers
        names =
        [
            "foo",
            "Contoso.BusinessLogic.DataAccess.Command",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "other_sql");
        }

        return testCases;
    }

    public static TheoryData<string, bool> IsSqlLikeProviderTestCases()
    {
        // Get all the possible names and assume they are false
        var values = DbSystemTestCases().ToDictionary((k) => (string)k.Data.Item1, (v) => false);

        // Override specific entries to be true
        string[] supported =
        [
            "Devart.Data.MySql.Entity.EFCore",
            "Devart.Data.MySql.MySqlCommand",
            "Devart.Data.Oracle.Entity.EFCore",
            "Devart.Data.Oracle.OracleCommand",
            "Devart.Data.PostgreSql.Entity.EFCore",
            "Devart.Data.PostgreSql.PgSqlCommand",
            "Devart.Data.SQLite.Entity.EFCore",
            "FirebirdSql.Data.FirebirdClient.FbCommand",
            "FirebirdSql.EntityFrameworkCore.Firebird",
            "Google.Cloud.EntityFrameworkCore.Spanner",
            "Google.Cloud.Spanner.Data.SpannerCommand",
            "IBM.EntityFrameworkCore",
            "IBM.EntityFrameworkCore-lnx",
            "IBM.EntityFrameworkCore-osx",
            "Microsoft.Data.SqlClient.SqlCommand",
            "Microsoft.Data.Sqlite.SqliteCommand",
            "Microsoft.EntityFrameworkCore.Sqlite",
            "Microsoft.EntityFrameworkCore.SqlServer",
            "MySql.Data.EntityFrameworkCore",
            "MySql.Data.MySqlClient.MySqlCommand",
            "MySql.EntityFrameworkCore",
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "Npgsql.NpgsqlCommand",
            "Oracle.EntityFrameworkCore",
            "Oracle.ManagedDataAccess.Client.OracleCommand",
            "Pomelo.EntityFrameworkCore.MySql",
            "Teradata.Client.Provider.TdCommand",
            "Teradata.EntityFrameworkCore",
        ];

        foreach (var name in supported)
        {
            values[name] = true;
        }

        var testCases = new TheoryData<string, bool>();

        foreach (var item in values)
        {
            testCases.Add(item.Key, item.Value);
        }

        return testCases;
    }

    public static TheoryData<string, bool> IsBackslashEscapeProviderTestCases()
    {
        var values = DbSystemTestCases().ToDictionary((k) => (string)k.Data.Item1, (v) => false);

        string[] backslashEscapeProviders =
        [
            "Devart.Data.MySql.Entity.EFCore",
            "Devart.Data.MySql.MySqlCommand",
            "MySql.Data.EntityFrameworkCore",
            "MySql.Data.MySqlClient.MySqlCommand",
            "MySql.EntityFrameworkCore",
            "Pomelo.EntityFrameworkCore.MySql",
        ];

        foreach (var name in backslashEscapeProviders)
        {
            values[name] = true;
        }

        var testCases = new TheoryData<string, bool>();

        foreach ((var name, var expected) in values)
        {
            testCases.Add(name, expected);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(DbSystemTestCases))]
    public void ShouldReturnCorrectAttributeValuesProviderOrCommandName(string name, string expectedDbSystemName)
    {
        var actualDbSystemName = EntityFrameworkDiagnosticListener.GetDbSystemName(name);

        Assert.Equal(expectedDbSystemName, actualDbSystemName);
    }

    [Theory]
    [MemberData(nameof(IsSqlLikeProviderTestCases))]
    public void ShouldReturnCorrectValueForSqlLikeProviderOrCommandName(string name, bool expected)
    {
        var actual = EntityFrameworkDiagnosticListener.IsSqlLikeProvider(name);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(IsBackslashEscapeProviderTestCases))]
    public void ShouldReturnCorrectValueForBackslashEscapeProviderOrCommandName(string name, bool expected)
    {
        var actual = EntityFrameworkDiagnosticListener.IsBackslashEscapeProvider(name);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EntityFrameworkContextEventsInstrumentedTest()
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation()
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            var items = context.Set<Item>().OrderBy(e => e.Name).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("ItemOne", items[0].Name);
            Assert.Equal("ItemThree", items[1].Name);
            Assert.Equal("ItemTwo", items[2].Name);
        }

        var activity = Assert.Single(exportedItems);

        VerifyActivityData(activity);
    }

    [Fact]
    public void EntityFrameworkEnrichDisplayNameWithEnrichWithIDbCommand()
    {
        var exportedItems = new List<Activity>();
        var expectedDisplayName = "Text main";

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options =>
                  {
                      options.EnrichWithIDbCommand = (activity1, command) =>
                      {
                          var stateDisplayName = $"{command.CommandType} main";
                          activity1.DisplayName = stateDisplayName;
                          activity1.SetTag(SemanticConventions.AttributeDbNamespace, stateDisplayName);
                      };
                  })
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            var items = context.Set<Item>().OrderBy(e => e.Name).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("ItemOne", items[0].Name);
            Assert.Equal("ItemThree", items[1].Name);
            Assert.Equal("ItemTwo", items[2].Name);
        }

        var activity = Assert.Single(exportedItems);

        VerifyActivityData(
            activity,
            expectedDisplayName: expectedDisplayName,
            expectedDbNamespace: expectedDisplayName);
    }

    [Fact]
    public void EntityFrameworkContextExceptionEventsInstrumentedTest()
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation()
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);

            try
            {
                context.Database.ExecuteSqlRaw("select * from no_table");
            }
            catch
            {
                // intentional empty catch
            }
        }

        var activity = Assert.Single(exportedItems);

        VerifyActivityData(activity, isError: true, expectedDisplayName: "select no_table");
    }

    [Fact]
    public async Task EntityFrameworkContextCommandCanceledEventStopsActivity()
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation()
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                await context.Database.ExecuteSqlRawAsync("select * from Item", cts.Token);
            }
            catch (OperationCanceledException)
            {
                // intentional empty catch
            }
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalseByDbCommand()
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options => options.Filter = (_, command) => !command.CommandText.Contains("Item", StringComparison.OrdinalIgnoreCase))
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrueByDbCommand()
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options => options.Filter = (_, command) => command.CommandText.Contains("Item", StringComparison.OrdinalIgnoreCase))
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        var activity = Assert.Single(exportedItems);

        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Microsoft.EntityFrameworkCore.Cosmos")]
    [InlineData("Devart.Data.SQLite.Entity.EFCore")]
    [InlineData("MySql.Data.EntityFrameworkCore")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql")]
    [InlineData("Devart.Data.MySql.Entity.EFCore")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Devart.Data.PostgreSql.Entity.EFCore")]
    [InlineData("Oracle.EntityFrameworkCore")]
    [InlineData("Devart.Data.Oracle.Entity.EFCore")]
    [InlineData("Microsoft.EntityFrameworkCore.InMemory")]
    [InlineData("FirebirdSql.EntityFrameworkCore.Firebird")]
    [InlineData("FileContextCore")]
    [InlineData("EntityFrameworkCore.SqlServerCompact35")]
    [InlineData("EntityFrameworkCore.SqlServerCompact40")]
    [InlineData("EntityFrameworkCore.OpenEdge")]
    [InlineData("EntityFrameworkCore.Jet")]
    [InlineData("Google.Cloud.EntityFrameworkCore.Spanner")]
    [InlineData("Teradata.EntityFrameworkCore")]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalseByProviderName(string provider)
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options => options.Filter = (providerName, _) => providerName != null && providerName.Equals(provider, StringComparison.OrdinalIgnoreCase))
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrueByProviderName()
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options => options.Filter = (providerName, _) => providerName != null && providerName.Equals("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase))
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();
        }

        var activity = Assert.Single(exportedItems);

        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    public void Dispose() => this.connection.Dispose();

    private static SqliteConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    private static void VerifyActivityData(
        Activity activity,
        bool isError = false,
        string expectedDisplayName = "SELECT",
        string expectedDbNamespace = "main")
    {
        Assert.Equal(expectedDisplayName, activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);

        Assert.Equal("sqlite", activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbSystemName).Value);
        Assert.DoesNotContain(activity.Tags, t => t.Key == SemanticConventions.AttributeDbSystem);

        Assert.Equal("OpenTelemetry.Instrumentation.EntityFrameworkCore", activity.Source.Name);
        Assert.NotNull(activity.Source.Version);
        Assert.NotEmpty(activity.Source.Version);

        Assert.Equal("https://opentelemetry.io/schemas/1.36.0", activity.Source.TelemetrySchemaUrl);

        // TBD: SqlLite not setting the DataSource so it doesn't get set.
        Assert.DoesNotContain(activity.Tags, t => t.Key == "peer.service");
        Assert.DoesNotContain(activity.Tags, t => t.Key == "server.address");
        Assert.DoesNotContain(activity.Tags, t => t.Key == "server.port");

        Assert.Equal(expectedDbNamespace, activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbNamespace).Value);
        Assert.DoesNotContain(activity.Tags, t => t.Key == SemanticConventions.AttributeDbName);

        if (!isError)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("SQLite Error 1: 'no such table: no_table'.", activity.StatusDescription);
        }
    }

    private void Seed()
    {
        using var context = new ItemsContext(this.contextOptions);

        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var one = new Item() { Name = "ItemOne" };

        var two = new Item() { Name = "ItemTwo" };

        var three = new Item() { Name = "ItemThree" };

        context.AddRange(one, two, three);

        context.SaveChanges();
    }
}
