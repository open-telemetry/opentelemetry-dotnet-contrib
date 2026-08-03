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

    public static TheoryData<string, string, string> DbSystemTestCases()
    {
        var testCases = new TheoryData<string, string, string>()
        {
            { "Microsoft.EntityFrameworkCore.Cosmos", "cosmosdb", "azure.cosmosdb" },
            { "MongoDB.EntityFrameworkCore", "mongodb", "mongodb" },
        };

        // Couchbase
        string[] names =
        [
            "Couchbase.EntityFrameworkCore",
            "Couchbase.EntityFrameworkCore.Storage.Internal",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "couchbase", "couchbase");
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
            testCases.Add(name, "db2", "ibm.db2");
        }

        // Firebird
        names =
        [
            "FirebirdSql.Data.FirebirdClient.FbCommand",
            "FirebirdSql.EntityFrameworkCore.Firebird",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "firebird", "firebirdsql");
        }

        // Microsoft SQL Server
        names =
        [
            "Microsoft.Data.SqlClient.SqlCommand",
            "Microsoft.EntityFrameworkCore.SqlServer",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "mssql", "microsoft.sql_server");
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
            testCases.Add(name, "mysql", "mysql");
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
            testCases.Add(name, "oracle", "oracle.db");
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
            testCases.Add(name, "postgresql", "postgresql");
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
            testCases.Add(name, "sqlite", "sqlite");
        }

        // Spanner
        names =
        [
            "Google.Cloud.EntityFrameworkCore.Spanner",
            "Google.Cloud.Spanner.Data.SpannerCommand",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "spanner", "gcp.spanner");
        }

        // Teradata
        names =
        [
            "Teradata.Client.Provider.TdCommand",
            "Teradata.EntityFrameworkCore",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "teradata", "teradata");
        }

        // Unknown providers
        names =
        [
            "foo",
            "Contoso.BusinessLogic.DataAccess.Command",
        ];

        foreach (var name in names)
        {
            testCases.Add(name, "other_sql", "other_sql");
        }

        return testCases;
    }

    public static TheoryData<string, bool> IsSqlLikeProviderTestCases()
    {
        // Get all the possible names and assume they are false
        var values = DbSystemTestCases().ToDictionary((k) => (string)k[0], (v) => false);

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

        foreach ((var name, var expected) in values)
        {
            testCases.Add(name, expected);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(DbSystemTestCases))]
    public void ShouldReturnCorrectAttributeValuesProviderOrCommandName(string name, string expectedDbSystem, string expectedDbSystemName)
    {
        (var actualDbSystem, var actualDbSystemName) = EntityFrameworkDiagnosticListener.GetDbSystemNames(name);

        Assert.Equal(expectedDbSystem, actualDbSystem);
        Assert.Equal(expectedDbSystemName, actualDbSystemName);
    }

    [Theory]
    [MemberData(nameof(IsSqlLikeProviderTestCases))]
    public void ShouldReturnCorrectValueForSqlLikeProviderOrCommandName(string name, bool expected)
    {
        var actual = EntityFrameworkDiagnosticListener.IsSqlLikeProvider(name);

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

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(activity);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EntityFrameworkEnrichDisplayNameWithEnrichWithIDbCommand(
        bool emitOldAttributes,
        bool emitNewAttributes)
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
                          activity1.SetTag("db.name", stateDisplayName);
                      };
                      options.EmitOldAttributes = emitOldAttributes;
                      options.EmitNewAttributes = emitNewAttributes;
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

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(
            activity,
            altDisplayName: expectedDisplayName,
            emitOldAttributes: emitOldAttributes,
            emitNewAttributes: emitNewAttributes);
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

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(activity, isError: true);
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

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

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

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldSanitizeQueryTextWithTheDefaultQueryTextSanitizer(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var activity = this.ExecuteQuery(
            "select * from Items where Name = 'ItemOne'",
            emitOldAttributes,
            emitNewAttributes,
            configure: null);

        var expectedQueryText = "select * from Items where Name = ?";

        if (emitOldAttributes)
        {
            Assert.Equal(expectedQueryText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        if (emitNewAttributes)
        {
            Assert.Equal(expectedQueryText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));

            var querySummary = activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary);
            Assert.NotNull(querySummary);
            Assert.NotEmpty((string)querySummary);
            Assert.Equal(querySummary, activity.DisplayName);
        }
        else
        {
            Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQuerySummary);
            Assert.Equal("main", activity.DisplayName);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldUseQueryTextFromQueryTextSanitizer(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var activity = this.ExecuteQuery(
            "select * from Items where Name = 'ItemOne'",
            emitOldAttributes,
            emitNewAttributes,
            options => options.QueryTextSanitizer = context =>
            {
                Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.ProviderName);
                Assert.Equal("select * from Items where Name = 'ItemOne'", context.QueryText);
                Assert.NotNull(context.Command);

                return QueryTextSanitizationResult.Sanitized("[redacted]");
            });

        if (emitOldAttributes)
        {
            Assert.Equal("[redacted]", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        if (emitNewAttributes)
        {
            Assert.Equal("[redacted]", activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
        }

        // No summary was supplied, so neither the summary nor the display name is set.
        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQuerySummary);
        Assert.Equal("main", activity.DisplayName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldUseQuerySummaryFromQueryTextSanitizer(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var activity = this.ExecuteQuery(
            "select * from Items where Name = 'ItemOne'",
            emitOldAttributes,
            emitNewAttributes,
            options => options.QueryTextSanitizer = _ => QueryTextSanitizationResult.Sanitized("[redacted]", "SELECT Items"));

        if (emitOldAttributes)
        {
            Assert.Equal("[redacted]", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        if (emitNewAttributes)
        {
            Assert.Equal("[redacted]", activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
            Assert.Equal("SELECT Items", activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary));
            Assert.Equal("SELECT Items", activity.DisplayName);
        }
        else
        {
            // The summary and the display name are only set by the new conventions.
            Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQuerySummary);
            Assert.Equal("main", activity.DisplayName);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldEmitOriginalQueryTextWhenQueryTextSanitizerReturnsNotSanitized(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var commandText = "select * from Items where Name = 'ItemOne'";

        var activity = this.ExecuteQuery(
            commandText,
            emitOldAttributes,
            emitNewAttributes,
            options => options.QueryTextSanitizer = _ => QueryTextSanitizationResult.NotSanitized);

        if (emitOldAttributes)
        {
            Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        if (emitNewAttributes)
        {
            Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
        }

        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQuerySummary);
        Assert.Equal("main", activity.DisplayName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldNotEmitQueryTextWhenQueryTextSanitizerReturnsNull(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var activity = this.ExecuteQuery(
            "select * from Items where Name = 'ItemOne'",
            emitOldAttributes,
            emitNewAttributes,
            options => options.QueryTextSanitizer = _ => QueryTextSanitizationResult.Sanitized(null, "SELECT Items"));

        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbStatement);
        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQueryText);

        if (emitNewAttributes)
        {
            Assert.Equal("SELECT Items", activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary));
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldEmitRawQueryTextWhenQueryTextSanitizerIsNull(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var commandText = "select * from Items where Name = 'ItemOne'";

        var activity = this.ExecuteQuery(
            commandText,
            emitOldAttributes,
            emitNewAttributes,
            options => options.QueryTextSanitizer = null);

        if (emitOldAttributes)
        {
            Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        if (emitNewAttributes)
        {
            Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
        }

        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQuerySummary);
        Assert.Equal("main", activity.DisplayName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ShouldNotEmitQueryTextWhenQueryTextSanitizerThrows(
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        var activity = this.ExecuteQuery(
            "select * from Items where Name = 'ItemOne'",
            emitOldAttributes,
            emitNewAttributes,
            options => options.QueryTextSanitizer = _ => throw new InvalidOperationException("Sanitization failed."));

        // The exception is swallowed and the command is still collected, but the
        // potentially sensitive query text is not emitted.
        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbStatement);
        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQueryText);
        Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeDbQuerySummary);

        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
        Assert.Equal("main", activity.DisplayName);
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
        string? altDisplayName = null,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        Assert.Equal(altDisplayName ?? "main", activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);

        if (emitOldAttributes)
        {
            Assert.Equal("sqlite", activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbSystem).Value);
        }

        if (emitNewAttributes)
        {
            Assert.Equal("sqlite", activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbSystemName).Value);
        }

        Assert.Equal("OpenTelemetry.Instrumentation.EntityFrameworkCore", activity.Source.Name);
        Assert.NotNull(activity.Source.Version);
        Assert.NotEmpty(activity.Source.Version);

        if (emitNewAttributes && emitOldAttributes)
        {
            Assert.Null(activity.Source.TelemetrySchemaUrl);
        }
        else if (emitOldAttributes)
        {
            Assert.Equal("https://opentelemetry.io/schemas/1.24.0", activity.Source.TelemetrySchemaUrl);
        }
        else if (emitNewAttributes)
        {
            Assert.Equal("https://opentelemetry.io/schemas/1.36.0", activity.Source.TelemetrySchemaUrl);
        }

        // TBD: SqlLite not setting the DataSource so it doesn't get set.
        Assert.DoesNotContain(activity.Tags, t => t.Key == "peer.service");
        Assert.DoesNotContain(activity.Tags, t => t.Key == "server.address");
        Assert.DoesNotContain(activity.Tags, t => t.Key == "server.port");

        if (emitOldAttributes)
        {
            Assert.Equal(altDisplayName ?? "main", activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbName).Value);
        }

        if (emitNewAttributes)
        {
            Assert.Equal("main", activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbNamespace).Value);
        }

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

    private Activity ExecuteQuery(
        string commandText,
        bool emitOldAttributes,
        bool emitNewAttributes,
        Action<EntityFrameworkInstrumentationOptions>? configure)
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options =>
                  {
                      configure?.Invoke(options);
                      options.EmitOldAttributes = emitOldAttributes;
                      options.EmitNewAttributes = emitNewAttributes;
                  })
                  .Build())
        {
            using var context = new ItemsContext(this.contextOptions);
            context.Database.ExecuteSqlRaw(commandText);
        }

        return Assert.Single(exportedItems);
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
