// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;
using OpenTelemetry.Tests;
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
        var values = DbSystemTestCases().ToDictionary((k) => k.Data.Item1, (v) => false);

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
        var values = DbSystemTestCases().ToDictionary((k) => k.Data.Item1, (v) => false);

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

    [Theory]
    [MemberData(nameof(IsBackslashEscapeProviderTestCases))]
    public void ShouldReturnCorrectValueForBackslashEscapeProviderOrCommandName(string name, bool expected)
    {
        var actual = EntityFrameworkDiagnosticListener.IsBackslashEscapeProvider(name);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(EntityFrameworkDiagnosticListener.EntityFrameworkCoreCommandCreated, true)]
    [InlineData(EntityFrameworkDiagnosticListener.EntityFrameworkCoreCommandExecuting, true)]
    [InlineData(EntityFrameworkDiagnosticListener.EntityFrameworkCoreCommandExecuted, true)]
    [InlineData(EntityFrameworkDiagnosticListener.EntityFrameworkCoreCommandCanceled, true)]
    [InlineData(EntityFrameworkDiagnosticListener.EntityFrameworkCoreCommandError, true)]
    [InlineData("Microsoft.EntityFrameworkCore.ChangeTracking.StartedTracking", false)]
    [InlineData("Microsoft.EntityFrameworkCore.ChangeTracking.DetectChangesStarting", false)]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command.DataReaderDisposing", false)]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Connection.ConnectionOpening", false)]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ContextInitialized", false)]
    [InlineData("Microsoft.EntityFrameworkCore.Query.QueryCompilationStarting", false)]
    public void SubscriptionIsOnlyEnabledForHandledEvents(string eventName, bool expected)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddEntityFrameworkCoreInstrumentation()
            .Build();

        // The instrumentation subscribes to every diagnostic listener with the EF Core name.
        using var listener = new DiagnosticListener(EntityFrameworkDiagnosticListener.DiagnosticSourceName);

        // EF Core checks whether an event is enabled using the single-argument overload.
        Assert.Equal(expected, listener.IsEnabled(eventName));
        Assert.Equal(expected, listener.IsEnabled(eventName, null, null));
        Assert.Equal(expected, EntityFrameworkDiagnosticListener.IsHandledEvent(eventName));
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

        var activity = Assert.Single(exportedItems);

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

        var activity = Assert.Single(exportedItems);

        VerifyActivityData(activity, isError: true);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldNotSuppressDownstreamSpansWhenCommandIsFilteredOut(bool filterThrows, bool useAsync)
    {
        var exportedItems = new List<Activity>();

        using var parentSource = new ActivitySource("Test.Parent");
        using var driverSource = new ActivitySource("Test.Driver");

        using (Sdk.CreateTracerProviderBuilder()
                  .AddSource(parentSource.Name, driverSource.Name)
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options =>
                      options.Filter = (_, _) => filterThrows ? throw new InvalidOperationException("Filter failed.") : false)
                  .Build())
        {
            var contextOptions = new DbContextOptionsBuilder<ItemsContext>()
                .UseSqlite(this.connection)
                .AddInterceptors(new DriverSpanInterceptor(driverSource))
                .Options;

            using (var parent = parentSource.StartActivity("parent"))
            {
                Assert.NotNull(parent);

                using var context = new ItemsContext(contextOptions);
                var query = context.Set<Item>().OrderBy(e => e.Name);
                _ = useAsync ? await query.ToListAsync(TestContext.Current.CancellationToken) : query.ToList();

                Assert.Same(parent, Activity.Current);
            }
        }

        Assert.DoesNotContain(exportedItems, a => a.Source.Name == EntityFrameworkDiagnosticListener.ActivitySource.Name);

        var parentActivity = Assert.Single(exportedItems, a => a.Source.Name == parentSource.Name);
        var driverActivity = Assert.Single(exportedItems, a => a.Source.Name == driverSource.Name);

        Assert.Equal(parentActivity.SpanId, driverActivity.ParentSpanId);
        Assert.True(driverActivity.Recorded);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShouldNotReportNullActivityWhenFilteredCommandHasNoParent(bool useAsync, bool commandFails)
    {
        var exportedItems = new List<Activity>();

        using var eventListener = new InMemoryEventListener(EntityFrameworkInstrumentationEventSource.Log);

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options => options.Filter = (_, _) => false)
                  .Build())
        {
            Assert.Null(Activity.Current);

            using var context = new ItemsContext(this.contextOptions);

            if (commandFails)
            {
                var exception = useAsync
                    ? await Record.ExceptionAsync(() => context.Database.ExecuteSqlRawAsync("select * from no_table", TestContext.Current.CancellationToken))
                    : Record.Exception(() => context.Database.ExecuteSqlRaw("select * from no_table"));

                Assert.IsType<SqliteException>(exception);
            }
            else
            {
                var query = context.Set<Item>().OrderBy(e => e.Name);
                _ = useAsync ? await query.ToListAsync(TestContext.Current.CancellationToken) : query.ToList();
            }
        }

        Assert.Empty(exportedItems);

        // 6 = CommandIsFilteredOut, 2 = NullActivity
        Assert.Contains(eventListener.Events, e => e.EventId == 6);
        Assert.DoesNotContain(eventListener.Events, e => e.EventId == 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldNotStopOuterActivityWhenNestedCommandIsFilteredOut(bool innerCommandFails)
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                  .AddInMemoryExporter(exportedItems)
                  .AddEntityFrameworkCoreInstrumentation(options =>
                      options.Filter = (_, command) => !command.CommandText.Contains("filtered", StringComparison.Ordinal))
                  .Build())
        {
            var innerCommandText = innerCommandFails
                ? "select * from no_table /* filtered */"
                : "select 1 /* filtered */";

            var interceptor = new NestedCommandInterceptor(this.contextOptions, innerCommandText);

            var contextOptions = new DbContextOptionsBuilder<ItemsContext>()
                .UseSqlite(this.connection)
                .AddInterceptors(interceptor)
                .Options;

            using var context = new ItemsContext(contextOptions);
            _ = context.Set<Item>().OrderBy(e => e.Name).ToList();

            // The outer command's activity must still be current and running once the inner command completed.
            Assert.Equal(EntityFrameworkDiagnosticListener.ActivitySource.Name, interceptor.SourceAfterInnerCommand);
            Assert.False(interceptor.StoppedAfterInnerCommand);
        }

        var activity = Assert.Single(exportedItems);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
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
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeErrorType));
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Null(activity.StatusDescription);
            Assert.Equal(typeof(SqliteException).FullName, activity.GetTagValue(SemanticConventions.AttributeErrorType));
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

    /// <summary>
    /// Runs another (filtered) command while the outer command is executing, then records the state of <see cref="Activity.Current"/>.
    /// </summary>
    private sealed class NestedCommandInterceptor(DbContextOptions<ItemsContext> innerContextOptions, string innerCommandText) : DbCommandInterceptor
    {
        public string? SourceAfterInnerCommand { get; private set; }

        public bool? StoppedAfterInnerCommand { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            using (var innerContext = new ItemsContext(innerContextOptions))
            {
                try
                {
                    innerContext.Database.ExecuteSqlRaw(innerCommandText);
                }
                catch (SqliteException)
                {
                    // Expected when the inner command is meant to fail
                }
            }

            this.SourceAfterInnerCommand = Activity.Current?.Source.Name;
            this.StoppedAfterInnerCommand = Activity.Current?.IsStopped;

            return base.ReaderExecuting(command, eventData, result);
        }
    }

    /// <summary>
    /// Starts a span while the command executes, like an instrumented ADO.NET provider (e.g. Npgsql) does.
    /// </summary>
    private sealed class DriverSpanInterceptor(ActivitySource activitySource) : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            using var activity = activitySource.StartActivity("driver", ActivityKind.Client);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            using var activity = activitySource.StartActivity("driver", ActivityKind.Client);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
