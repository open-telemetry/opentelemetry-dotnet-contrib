// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
[Trait("CategoryName", "SqlIntegrationTests")]
public sealed class SqlClientIntegrationTests :
    IClassFixture<SqlClientIntegrationTestsFixture>,
    IClassFixture<WeaverFixture>
{
    private const string GetContextInfoQuery = "SELECT CONTEXT_INFO()";

#if NET
    private const string ReadSessionStateProcedureName = "dbo.otel_read_session_state";
    private const string CreateReadSessionStateProcedureQuery = "CREATE OR ALTER PROCEDURE " + ReadSessionStateProcedureName + " AS SELECT TOP 1 c.connection_id, CONTEXT_INFO() FROM sys.dm_exec_connections AS c WHERE c.session_id = @@SPID";
#endif

    private readonly ITestOutputHelper outputHelper;
    private readonly SqlClientIntegrationTestsFixture sqlServer;
    private readonly WeaverFixture weaver;

    public SqlClientIntegrationTests(
        SqlClientIntegrationTestsFixture sqlServer,
        WeaverFixture weaver,
        ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
        this.sqlServer = sqlServer;
        this.weaver = weaver;
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData(CommandType.Text, "select 1/1", "select ?/?")]
    [InlineData(CommandType.Text, "select 1/0", "select ?/?", true)]
    [InlineData(CommandType.Text, "select 1/0", "select ?/?", true, true)]
#if NET
    [InlineData(CommandType.Text, GetContextInfoQuery, GetContextInfoQuery, false, false, false)]
    [InlineData(CommandType.Text, GetContextInfoQuery, GetContextInfoQuery, false, false, true)]
    [InlineData(CommandType.StoredProcedure, "sp_who", "sp_who")]
#endif
    [InlineData(CommandType.Text, "exec sp_who", "exec sp_who")]
    public async Task SuccessfulCommandTest(
        CommandType commandType,
        string commandText,
        string? sanitizedCommandText,
        bool isFailure = false,
        bool recordException = false,
        bool enableTransaction = false)
    {
        using var scope = EnvironmentVariableScope.Create(
            SqlClientTraceInstrumentationOptions.ContextPropagationLevelEnvVar,
            commandText == GetContextInfoQuery ? "true" : null);

#if NETFRAMEWORK
        // Disable things not available on netfx
        recordException = false;
#endif

        var sampler = new TestSampler();
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation(options =>
            {
#if NET
                options.RecordException = recordException;
#endif
            })
            .Build();

        using var sqlConnection = new SqlConnection(this.GetConnectionString());

        sqlConnection.Open();

        sqlConnection.ChangeDatabase("master");
        SqlTransaction? transaction = null;
#pragma warning disable CA2100
        using var sqlCommand = new SqlCommand(commandText, sqlConnection)
#pragma warning restore CA2100
        {
            CommandType = commandType,
        };

        if (enableTransaction)
        {
            transaction = sqlConnection.BeginTransaction();
            sqlCommand.Transaction = transaction;
        }

        object commandResult = DBNull.Value;
        try
        {
            commandResult = sqlCommand.ExecuteScalar();
        }
        catch (Exception)
        {
        }

        transaction?.Commit();

        var activity = Assert.Single(activities);

        VerifyContextInfo(commandText, commandResult, activity);
        VerifyActivityData(commandType, sanitizedCommandText, isFailure, recordException, activity);
        VerifySamplingParameters(sampler.LatestSamplingParameters);

        if (isFailure)
        {
#if NET
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("Divide by zero error encountered.", activity.StatusDescription);
            Assert.EndsWith("SqlException", activity.GetTagValue(SemanticConventions.AttributeErrorType) as string);
            Assert.Equal("8134", activity.GetTagValue(SemanticConventions.AttributeDbResponseStatusCode));
#else
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("8134", activity.StatusDescription);
            Assert.EndsWith("SqlException", activity.GetTagValue(SemanticConventions.AttributeErrorType) as string);
            Assert.Equal("8134", activity.GetTagValue(SemanticConventions.AttributeDbResponseStatusCode));
#endif
        }

        await WeaverTelemetryVerifier.VerifyAsync(
            (activities, []),
            SqlTelemetryHelper.SemanticConventionsVersion,
            this.weaver,
            this.outputHelper);
    }

#if NET
    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task SuccessfulParameterizedQueryTest()
    {
        // Arrange
        var sampler = new TestSampler();
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation(options => options.SetDbQueryParameters = true)
            .Build();

        using var sqlConnection = new SqlConnection(this.GetConnectionString());

        await sqlConnection.OpenAsync();

        sqlConnection.ChangeDatabase("master");

        using var sqlCommand = new SqlCommand("SELECT @x + @y + @z", sqlConnection);

        sqlCommand.Parameters.AddWithValue("@x", 42);
        sqlCommand.Parameters.AddWithValue("@y", 37);
        sqlCommand.Parameters.AddWithValue("@z", 1234.56);

        // Act
        var result = await sqlCommand.ExecuteScalarAsync();

        // Assert
        Assert.Equal(1313.56, result);

        var activity = Assert.Single(activities);

        Assert.Equal("42", activity.GetTagValue("db.query.parameter.@x"));
        Assert.Equal("37", activity.GetTagValue("db.query.parameter.@y"));
        Assert.Equal("1234.56", activity.GetTagValue("db.query.parameter.@z"));

        await WeaverTelemetryVerifier.VerifyAsync(
            (activities, []),
            SqlTelemetryHelper.SemanticConventionsVersion,
            this.weaver,
            this.outputHelper,
            [new("invalid_format", null)]); // See https://github.com/open-telemetry/weaver/issues/1443
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ContextInfoIsClearedWhenPooledConnectionIsReused()
    {
        // Arrange
        using var scope = EnvironmentVariableScope.Create(
            SqlClientTraceInstrumentationOptions.ContextPropagationLevelEnvVar,
            "true");

        await this.CreateReadSessionStateProcedureAsync();

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation()
            .Build();

        // A distinct connection string is a distinct pool key, so this gives the test a pool
        // that can hold exactly one physical connection. The reuse below is then guaranteed
        // rather than dependent on the order the driver pops connections off its own pool.
        var connectionStringBuilder = new SqlConnectionStringBuilder(this.GetConnectionString())
        {
            MaxPoolSize = 1,
        };

        Guid connectionId;

        // Act
        using (var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString))
        {
            await sqlConnection.OpenAsync();

            sqlConnection.ChangeDatabase("master");

            using (var sqlCommand = new SqlCommand("select 1", sqlConnection))
            {
                await sqlCommand.ExecuteScalarAsync();
            }

            var textActivity = Assert.Single(activities);

            var sessionState = await ReadSessionStateAsync(sqlConnection);

            connectionId = sessionState.ConnectionId;

            // The text command wrote its traceparent, so the assertion after the pooled
            // reuse below is about the reset and not about propagation never having run.
            Assert.Equal(textActivity.Id, sessionState.ContextInfo);
        }

        using (var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString))
        {
            await sqlConnection.OpenAsync();

            sqlConnection.ChangeDatabase("master");

            var sessionState = await ReadSessionStateAsync(sqlConnection);

            // Assert
            Assert.Equal(connectionId, sessionState.ConnectionId);
            Assert.Null(sessionState.ContextInfo);
        }

        Assert.Equal(3, activities.Count);

        await WeaverTelemetryVerifier.VerifyAsync(
            (activities, []),
            SqlTelemetryHelper.SemanticConventionsVersion,
            this.weaver,
            this.outputHelper);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task StoredProcedureObservesContextInfoOfPrecedingTextCommand()
    {
        // Arrange
        using var scope = EnvironmentVariableScope.Create(
            SqlClientTraceInstrumentationOptions.ContextPropagationLevelEnvVar,
            "true");

        await this.CreateReadSessionStateProcedureAsync();

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation()
            .Build();

        using var sqlConnection = new SqlConnection(this.GetConnectionString());

        await sqlConnection.OpenAsync();

        sqlConnection.ChangeDatabase("master");

        // Act
        using (var sqlCommand = new SqlCommand("select 1", sqlConnection))
        {
            await sqlCommand.ExecuteScalarAsync();
        }

        var textActivity = Assert.Single(activities);

        // The stopped activity is left as Activity.Current by the async command, which would
        // make the stored procedure a child of it. Clear it so the two commands belong to
        // unrelated traces, which is the case the stale value actually mis-attributes.
        Activity.Current = null;

        var sessionState = await ReadSessionStateAsync(sqlConnection);

        // Assert
        Assert.Equal(2, activities.Count);

        var storedProcedureActivity = activities[1];

        Assert.NotEqual(textActivity.TraceId, storedProcedureActivity.TraceId);
        Assert.Equal(textActivity.Id, sessionState.ContextInfo);
        Assert.NotEqual(storedProcedureActivity.Id, sessionState.ContextInfo);

        await WeaverTelemetryVerifier.VerifyAsync(
            (activities, []),
            SqlTelemetryHelper.SemanticConventionsVersion,
            this.weaver,
            this.outputHelper);
    }
#endif

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ActivityIsStoppedWhenOnlyUsingMetrics()
    {
        // Arrange
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using var listener = new ActivityListener();
        listener.ActivityStarted = activities.Add;
        listener.Sample = (ref _) => ActivitySamplingResult.AllData;
        listener.ShouldListenTo = _ => true;

        ActivitySource.AddActivityListener(listener);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .AddSqlClientInstrumentation()
            .Build();

        using var sqlConnection = new SqlConnection(this.GetConnectionString());

        await sqlConnection.OpenAsync();

        sqlConnection.ChangeDatabase("master");

        using var sqlCommand = new SqlCommand("select 1/1", sqlConnection);

        // Act
        var result = await sqlCommand.ExecuteScalarAsync();

        meterProvider.ForceFlush();

        // Assert
        Assert.Equal(1, result);

        var activity = Assert.Single(activities);

        Assert.True(activity.IsStopped);

        await WeaverTelemetryVerifier.VerifyAsync(
            ([], metrics),
            SqlTelemetryHelper.SemanticConventionsVersion,
            this.weaver,
            this.outputHelper);
    }

    private static void VerifyContextInfo(
        string? commandText,
        object commandResult,
        Activity activity)
    {
        if (commandText == GetContextInfoQuery)
        {
            Assert.NotEqual(commandResult, DBNull.Value);
            Assert.True(commandResult is byte[]);
            var contextInfo = Encoding.ASCII.GetString((byte[])commandResult).TrimEnd('\0');
            Assert.Equal(contextInfo, activity.Id);
        }
    }

    private static void VerifyActivityData(
        CommandType commandType,
        string? commandText,
        bool isFailure,
        bool recordException,
        Activity activity)
    {
        var dbQuerySummary = activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary);
        Assert.Equal(dbQuerySummary, activity.DisplayName);

        Assert.Equal(ActivityKind.Client, activity.Kind);

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);

            if (recordException)
            {
                var events = activity.Events.ToList();
                Assert.Single(events);

                Assert.Equal(SemanticConventions.AttributeExceptionEventName, events[0].Name);
            }
            else
            {
                Assert.Empty(activity.Events);
            }
        }

        Assert.Equal(SqlTelemetryHelper.MicrosoftSqlServerDbSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystemName));
        Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));

        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal));
        Assert.DoesNotContain(activity.Tags, tag => tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal));

        switch (commandType)
        {
            case CommandType.StoredProcedure:
                Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStoredProcedureName));
                break;
            case CommandType.Text:
                Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
                break;
            case CommandType.TableDirect:
                Assert.Fail("Not supported command type: CommandType.TableDirect");
                break;
            default:
                Assert.Fail($"Not supported command type: {commandType}");
                break;
        }
    }

    private static void VerifySamplingParameters(SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystemName
                   && kvp.Value != null
                   && (string)kvp.Value == SqlTelemetryHelper.MicrosoftSqlServerDbSystemName);
    }

#if NET
    private static async Task<(Guid ConnectionId, string? ContextInfo)> ReadSessionStateAsync(SqlConnection sqlConnection)
    {
        // The read has to be a stored procedure: propagation only fires for CommandType.Text,
        // so a text command would overwrite CONTEXT_INFO before it could be read back.
        using var sqlCommand = new SqlCommand(ReadSessionStateProcedureName, sqlConnection)
        {
            CommandType = CommandType.StoredProcedure,
        };

        using var reader = await sqlCommand.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        var connectionId = reader.GetGuid(0);
        var contextInfo = reader.IsDBNull(1)
            ? null
            : Encoding.ASCII.GetString((byte[])reader[1]).TrimEnd('\0');

        return (connectionId, contextInfo);
    }
#endif

    private string GetConnectionString()
        => this.sqlServer.TypedContainer.GetConnectionString();

#if NET
    private async Task CreateReadSessionStateProcedureAsync()
    {
        // Pooling is disabled so that the setup connection cannot be the one the pool
        // hands back to the test, which asserts on a specific physical connection.
        var connectionStringBuilder = new SqlConnectionStringBuilder(this.GetConnectionString())
        {
            Pooling = false,
        };

        using var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);

        await sqlConnection.OpenAsync();

        sqlConnection.ChangeDatabase("master");

        using var sqlCommand = new SqlCommand(CreateReadSessionStateProcedureQuery, sqlConnection);

        await sqlCommand.ExecuteNonQueryAsync();
    }
#endif
}
