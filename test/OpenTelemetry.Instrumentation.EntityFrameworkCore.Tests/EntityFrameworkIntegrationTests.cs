// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Npgsql;
using OpenTelemetry.Instrumentation.SqlClient.Tests;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Testcontainers.MsSql;
using Testcontainers.SqlEdge;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

[Trait("CategoryName", "SqlIntegrationTests")]
public sealed class EntityFrameworkIntegrationTests :
    IClassFixture<MySqlIntegrationTestsFixture>,
    IClassFixture<PostgresIntegrationTestsFixture>,
    IClassFixture<SqlClientIntegrationTestsFixture>
{
    private const string MySqlProvider = "Pomelo.EntityFrameworkCore.MySql";
    private const string PostgresProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    private const string ActivitySourceName = "OpenTelemetry.Instrumentation.EntityFrameworkCore";

    private readonly MySqlIntegrationTestsFixture mySqlFixture;
    private readonly PostgresIntegrationTestsFixture postgresFixture;
    private readonly SqlClientIntegrationTestsFixture sqlServerFixture;

    public EntityFrameworkIntegrationTests(
        MySqlIntegrationTestsFixture mySqlFixture,
        PostgresIntegrationTestsFixture postgresFixture,
        SqlClientIntegrationTestsFixture fixture)
    {
        this.mySqlFixture = mySqlFixture;
        this.postgresFixture = postgresFixture;
        this.sqlServerFixture = fixture;
    }

    public static TheoryData<string, string, bool, bool, bool, Type, string, string, string, string?> RawSqlTestCases()
    {
        (string, Type, bool, string, string)[] providers =
        [
            (MySqlProvider, typeof(MySqlCommand), false, "mysql", "test"),
            (MySqlProvider, typeof(MySqlCommand), true, "mysql", "test"),
            (PostgresProvider, typeof(NpgsqlCommand), false, "postgresql", "postgres"),
            (PostgresProvider, typeof(NpgsqlCommand), true, "postgresql", "postgres"),
            (SqliteProvider, typeof(SqliteCommand), false, "sqlite", "main"),
            (SqliteProvider, typeof(SqliteCommand), true, "sqlite", "main"),
            (SqlServerProvider, typeof(SqlCommand), false, "mssql", "master"),
            (SqlServerProvider, typeof(SqlCommand), true, "microsoft.sql_server", "master"),
        ];

        var testCases = new TheoryData<string, string, bool, bool, bool, Type, string, string, string, string?>();

        foreach ((var provider, var commandType, var useNewConventions, var system, var database) in providers)
        {
            var expectedSpanNameWhenCaptureTextCommandContent = database;

            testCases.Add(provider, "select 1/1", false, false, useNewConventions, commandType, database, system, database, null);
            testCases.Add(provider, "select 1/1", true, false, useNewConventions, commandType, expectedSpanNameWhenCaptureTextCommandContent, system, database, null);

            // For some reason, SQLite does not throw an exception for division by zero
            // TODO Remove the second part of the conditions when EFCore sets SemanticConventions.AttributeDbQuerySummary
            // so that there isn't a drift between the expected span names used between SQL Server and EFCore
            if (provider == PostgresProvider && !useNewConventions)
            {
                testCases.Add(provider, "select 1/0", false, true, useNewConventions, commandType, database, system, database, "22012: division by zero");
                testCases.Add(provider, "select 1/0", true, true, useNewConventions, commandType, expectedSpanNameWhenCaptureTextCommandContent, system, database, "22012: division by zero");
            }
            else if (provider == SqlServerProvider && !useNewConventions)
            {
                testCases.Add(provider, "select 1/0", false, true, useNewConventions, commandType, database, system, database, "Divide by zero error encountered.");
                testCases.Add(provider, "select 1/0", true, true, useNewConventions, commandType, expectedSpanNameWhenCaptureTextCommandContent, system, database, "Divide by zero error encountered.");
            }
        }

        return testCases;
    }

    public static TheoryData<string, bool, bool, bool, Type, string, string> DataContextTestCases()
    {
        (string, Type, string, string)[] providers =
        [
            (MySqlProvider, typeof(MySqlCommand), "mysql", "test"),
            (PostgresProvider, typeof(NpgsqlCommand), "postgresql", "postgres"),
            (SqliteProvider, typeof(SqliteCommand), "sqlite", "main"),
            (SqlServerProvider, typeof(SqlCommand), "mssql", "master"),
        ];

        bool[] trueFalse = [true, false];

        var testCases = new TheoryData<string, bool, bool, bool, Type, string, string>();

        foreach ((var provider, var commandType, var system, var database) in providers)
        {
            foreach (var captureTextCommandContent in trueFalse)
            {
                foreach (var shouldEnrich in trueFalse)
                {
                    testCases.Add(provider, captureTextCommandContent, shouldEnrich, false, commandType, system, database);
                }
            }
        }

        return testCases;
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [MemberData(nameof(RawSqlTestCases))]
    public async Task TracesRawSql(
        string provider,
        string commandText,
        bool captureTextCommandContent,
        bool isFailure,
        bool useNewConventions,
        Type expectedCommandType,
        string expectedSpanName,
        string expectedSystemName,
        string expectedDatabaseName,
        string? expectedStatusDescription)
    {
        var conventions = useNewConventions ? SemanticConvention.New : SemanticConvention.Old;

        // In the case of SQL Server, the activity we're interested in is the one
        // created by the SqlClient instrumentation which is a child of EFCore.
        var expectedSourceName = isFailure && provider is SqlServerProvider
            ? "OpenTelemetry.Instrumentation.SqlClient"
            : ActivitySourceName;

        var filtered = false;

        bool ActivityFilter(string? providerName, IDbCommand command)
        {
            filtered = true;

            Assert.True(providerName == provider || providerName == null, $"The provider name {providerName} is not null or the expected value.");
            Assert.IsType(expectedCommandType, command, false);

            return true;
        }

        using var scope = SemanticConventionScope.Get(useNewConventions);

        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureTextCommandContent;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = ActivityFilter;
                options.SetDbStatementForText = captureTextCommandContent;
            })
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ItemsContext>();

        this.ConfigureProvider(provider, optionsBuilder);

        await using var context = new ItemsContext(optionsBuilder.Options);

        try
        {
            await context.Database.ExecuteSqlRawAsync(commandText);
        }
        catch
        {
            // Ignore
        }

        Assert.NotEmpty(activities);

        // All activities should either be the root EFCore activity or a child of it.
        Assert.All(activities, activity => Assert.Equal(ActivitySourceName, (activity.Parent?.Source ?? activity.Source).Name));

        var activity = activities.FirstOrDefault((p) => p.Source.Name == expectedSourceName);
        Assert.NotNull(activity);

        VerifyActivityData(
            captureTextCommandContent,
            isFailure,
            conventions,
            expectedSpanName,
            expectedSystemName,
            expectedDatabaseName,
            activity);

        if (isFailure)
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal(expectedStatusDescription, activity.StatusDescription);
        }

        Assert.True(filtered);

        if (!isFailure && provider is SqlServerProvider)
        {
            Assert.Contains(
                activities,
                activity => activity.Tags.Any(t => t.Key == conventions.Database));

            if (conventions.ServerPort is not null)
            {
                Assert.Contains(
                    activities,
                    activity => activity.TagObjects.Any(t => t.Key == conventions.ServerPort));
            }
        }
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [MemberData(nameof(DataContextTestCases))]
    public async Task TracesDataContext(
        string provider,
        bool captureTextCommandContent,
        bool shouldEnrich,
        bool useNewConventions,
        Type expectedCommandType,
        string expectedSystemName,
        string expectedDatabaseName)
    {
        var conventions = useNewConventions ? SemanticConvention.New : SemanticConvention.Old;
        var enriched = false;

        void ActivityEnrichment(Activity activity, IDbCommand command)
        {
            enriched = true;

            activity.SetTag("enriched", "yes");
            Assert.IsType(expectedCommandType, command, false);
        }

        var filtered = false;

        bool ActivityFilter(string? providerName, IDbCommand command)
        {
            filtered = true;

            Assert.True(providerName == provider || providerName == null, $"The provider name {providerName} is not null or the expected value.");
            Assert.IsType(expectedCommandType, command, false);

            return true;
        }

        using var scope = SemanticConventionScope.Get(useNewConventions);

        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureTextCommandContent;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.Filter = ActivityFilter;
                options.SetDbStatementForText = captureTextCommandContent;
                if (shouldEnrich)
                {
                    options.EnrichWithIDbCommand = ActivityEnrichment;
                }
            })
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ItemsContext>();

        this.ConfigureProvider(provider, optionsBuilder);

        await using var context = new ItemsContext(optionsBuilder.Options);
        await context.Database.EnsureCreatedAsync();

        // Clear activities from creating the database
        activities.Clear();

        var result = await context.Items.ToListAsync();

        Assert.NotNull(result);
        Assert.Empty(result);

        // All activities should either be the root EFCore activity or a child of it.
        Assert.All(activities, activity => Assert.Equal(ActivitySourceName, (activity.Parent?.Source ?? activity.Source).Name));

        // When using SQL Server there may be multiple activities, but we care
        // about the EFCore activity which should be the parent activity.
        var activity = Assert.Single(activities, activity => activity.Parent is null);

        Assert.Equal(ActivitySourceName, activity.Source.Name);
        Assert.Null(activity.Parent);

        Assert.Equal(expectedSystemName, activity.GetTagValue(conventions.System));
        Assert.Equal(expectedDatabaseName, activity.GetTagValue(conventions.Database));

        Assert.Equal(shouldEnrich, enriched);
        Assert.True(filtered);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData(SqliteProvider)]
    [InlineData(SqlServerProvider)]
    public async Task SuccessfulParameterizedQueryTest(string provider)
    {
        // Arrange
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddEntityFrameworkCoreInstrumentation(options => options.SetDbQueryParameters = true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ItemsContext>();

        this.ConfigureProvider(provider, optionsBuilder);

        await using var context = new ItemsContext(optionsBuilder.Options);
        await context.Database.EnsureCreatedAsync();

        // Clear activities from creating the database
        activities.Clear();

        // Act
        await context.Database.ExecuteSqlRawAsync(
            "SELECT @x + @y",
            CreateParameter(provider, "@x", 42),
            CreateParameter(provider, "@y", 37));

        // Assert
        var activity = Assert.Single(activities);

        Assert.Equal(42, activity.GetTagValue("db.query.parameter.@x"));
        Assert.Equal(37, activity.GetTagValue("db.query.parameter.@y"));
    }

    private static object CreateParameter(string provider, string name, object value)
    {
        return provider switch
        {
            SqliteProvider => new SqliteParameter(name, value),
            SqlServerProvider => new SqlParameter(name, value),
            _ => throw new NotSupportedException($"Unsupported provider: {provider}"),
        };
    }

    private static void VerifyActivityData(
        bool captureTextCommandContent,
        bool isFailure,
        SemanticConvention conventions,
        string expectedSpanName,
        string expectedSystemName,
        string expectedDatabaseName,
        Activity activity)
    {
        Assert.Equal(expectedSpanName, activity.DisplayName);

        Assert.Equal(ActivityKind.Client, activity.Kind);

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);
            Assert.Empty(activity.Events);
        }

        Assert.Equal(expectedSystemName, activity.GetTagValue(conventions.System));
        Assert.Equal(expectedDatabaseName, activity.GetTagValue(conventions.Database));

        if (captureTextCommandContent)
        {
            Assert.NotNull(activity.GetTagValue(conventions.QueryText));
        }
        else
        {
            Assert.Null(activity.GetTagValue(conventions.QueryText));
        }

        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal));
        Assert.DoesNotContain(activity.Tags, tag => tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal));
    }

    private string GetSqlServerConnectionString() => this.sqlServerFixture.DatabaseContainer switch
    {
        SqlEdgeContainer container => container.GetConnectionString(),
        MsSqlContainer container => container.GetConnectionString(),
        _ => throw new InvalidOperationException($"Container type '${this.sqlServerFixture.DatabaseContainer.GetType().Name}' is not supported."),
    };

    private void ConfigureProvider(string provider, DbContextOptionsBuilder<ItemsContext> builder)
    {
        switch (provider)
        {
            case MySqlProvider:
                var connectionString = this.mySqlFixture.DatabaseContainer.GetConnectionString();
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;

            case PostgresProvider:
                builder.UseNpgsql(this.postgresFixture.DatabaseContainer.GetConnectionString());
                break;

            case SqliteProvider:
                var file = Path.GetTempFileName();
                builder.UseSqlite($"Filename={file}");
                break;

            case SqlServerProvider:
                builder.UseSqlServer(this.GetSqlServerConnectionString());
                break;

            default:
                throw new NotSupportedException($"Unsupported provider: {provider}");
        }
    }

    private sealed class SemanticConvention
    {
        public static SemanticConvention Old { get; } = new SemanticConvention
        {
            EmitsNewAttributes = false,
            Database = SemanticConventions.AttributeDbName,
            QueryText = SemanticConventions.AttributeDbStatement,
            ServerAddress = "peer.service",
            ServerPort = null,
            System = SemanticConventions.AttributeDbSystem,
        };

        public static SemanticConvention New { get; } = new SemanticConvention
        {
            EmitsNewAttributes = true,
            Database = SemanticConventions.AttributeDbNamespace,
            QueryText = SemanticConventions.AttributeDbQueryText,
            ServerAddress = SemanticConventions.AttributeServerAddress,
            ServerPort = SemanticConventions.AttributeServerPort,
            System = SemanticConventions.AttributeDbSystemName,
        };

        public bool EmitsNewAttributes { get; private init; }

        public required string Database { get; init; }

        public required string QueryText { get; init; }

        public required string ServerAddress { get; init; }

        public required string? ServerPort { get; init; }

        public required string System { get; init; }
    }

    private sealed class SemanticConventionScope(string? previous) : IDisposable
    {
        private const string ConventionsOptIn = "OTEL_SEMCONV_STABILITY_OPT_IN";

        public static SemanticConventionScope Get(bool useNewConventions)
        {
            var previous = Environment.GetEnvironmentVariable(ConventionsOptIn);

            Environment.SetEnvironmentVariable(
                ConventionsOptIn,
                useNewConventions ? "database" : string.Empty);

            return new SemanticConventionScope(previous);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(ConventionsOptIn, previous);
    }
}
