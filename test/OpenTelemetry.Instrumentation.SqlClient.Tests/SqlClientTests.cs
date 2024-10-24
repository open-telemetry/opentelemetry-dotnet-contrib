// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
#if !NETFRAMEWORK
using OpenTelemetry.Tests;
#endif
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
public class SqlClientTests : IDisposable
{
#if !NETFRAMEWORK
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master";
#endif

    private readonly FakeSqlClientDiagnosticSource fakeSqlClientDiagnosticSource;

    public SqlClientTests()
    {
        this.fakeSqlClientDiagnosticSource = new FakeSqlClientDiagnosticSource();
    }

    public void Dispose()
    {
        this.fakeSqlClientDiagnosticSource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SqlClient_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddSqlClientInstrumentation());
    }

    [Fact]
    public void SqlClient_NamedOptions()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<SqlClientTraceInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<SqlClientTraceInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddSqlClientInstrumentation()
            .AddSqlClientInstrumentation("Instrumentation2", configureSqlClientTraceInstrumentationOptions: null)
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    // DiagnosticListener-based instrumentation is only available on .NET Core
#if !NETFRAMEWORK
    [Theory]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false, false)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false, false)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true, false)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true, false)]

    // Test cases when EmitOldAttributes = false and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database)
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false, true, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false, false, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false, true, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false, false, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true, true, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true, false, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true, true, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true, false, false, true)]

    // Test cases when EmitOldAttributes = true and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database/dup)
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false, true, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false, false, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false, true, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false, false, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true, true, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true, false, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true, true, true, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true, false, true, true)]
    public void SqlClientCallsAreCollectedSuccessfully(
        string beforeCommand,
        string afterCommand,
        CommandType commandType,
        string commandText,
        bool captureStoredProcedureCommandName,
        bool captureTextCommandContent,
        bool shouldEnrich = true,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
                .AddSqlClientInstrumentation(
                    (opt) =>
                    {
                        opt.SetDbStatementForText = captureTextCommandContent;
                        opt.SetDbStatementForStoredProcedure = captureStoredProcedureCommandName;
                        if (shouldEnrich)
                        {
                            opt.Enrich = ActivityEnrichment;
                        }

                        opt.EmitOldAttributes = emitOldAttributes;
                        opt.EmitNewAttributes = emitNewAttributes;
                    })
                .AddInMemoryExporter(activities)
                .Build())
        {
            var operationId = Guid.NewGuid();
            sqlCommand.CommandType = commandType;
#pragma warning disable CA2100
            sqlCommand.CommandText = commandText;
#pragma warning restore CA2100

            var beforeExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = (long?)1000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                beforeCommand,
                beforeExecuteEventData);

            var afterExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = 2000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                afterCommand,
                afterExecuteEventData);
        }

        Assert.Single(activities);
        var activity = activities[0];

        VerifyActivityData(
            sqlCommand.CommandType,
            sqlCommand.CommandText,
            captureStoredProcedureCommandName,
            captureTextCommandContent,
            false,
            false,
            shouldEnrich,
            activity,
            emitOldAttributes,
            emitNewAttributes);
    }

    [Theory]
    [InlineData(true, "localhost", "localhost", null, null, null)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, 1434)]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", 1818)]
    [InlineData(false, "localhost", null, null, null, null)]

    // Test cases when EmitOldAttributes = false and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database)
    [InlineData(true, "localhost", "localhost", null, null, null, false, true)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null, false, true)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, 1434, false, true)]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", null, 1818, false, true)]
    [InlineData(false, "localhost", null, null, null, null, false, true)]

    // Test cases when EmitOldAttributes = true and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database/dup)
    [InlineData(true, "localhost", "localhost", null, null, null, true, true)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null, true, true)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, 1434, true, true)]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", 1818, true, true)]
    [InlineData(false, "localhost", null, null, null, null, true, true)]
    public void SqlClientAddsConnectionLevelAttributes(
        bool enableConnectionLevelAttributes,
        string dataSource,
        string? expectedServerHostName,
        string? expectedServerIpAddress,
        string? expectedInstanceName,
        int? expectedPort,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        var activity = new Activity("Test Sql Activity");
        var options = new SqlClientTraceInstrumentationOptions()
        {
            EnableConnectionLevelAttributes = enableConnectionLevelAttributes,
            EmitOldAttributes = emitOldAttributes,
            EmitNewAttributes = emitNewAttributes,
        };
        SqlActivitySourceHelper.AddConnectionLevelDetailsToActivity(dataSource, activity, options);

        Assert.Equal(expectedServerHostName ?? expectedServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));

        if (emitOldAttributes)
        {
            Assert.Equal(expectedInstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        }
        else
        {
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        }

        Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));
    }

    [Theory]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError, false)]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError, false, true)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError, false)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError, false, true)]
    public void SqlClientErrorsAreCollectedSuccessfully(string beforeCommand, string errorCommand, bool shouldEnrich = true, bool recordException = false)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordException = recordException;
                if (shouldEnrich)
                {
                    options.Enrich = ActivityEnrichment;
                }
            })
            .AddInMemoryExporter(activities)
            .Build())
        {
            var operationId = Guid.NewGuid();
            sqlCommand.CommandText = "SP_GetOrders";
            sqlCommand.CommandType = CommandType.StoredProcedure;

            var beforeExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = (long?)1000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                beforeCommand,
                beforeExecuteEventData);

            var commandErrorEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Exception = new Exception("Boom!"),
                Timestamp = 2000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                errorCommand,
                commandErrorEventData);
        }

        Assert.Single(activities);
        var activity = activities[0];

        VerifyActivityData(
            sqlCommand.CommandType,
            sqlCommand.CommandText,
            true,
            false,
            true,
            recordException,
            shouldEnrich,
            activity);
    }

    [Theory]
    [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand)]
    [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand)]
    public void SqlClientCreatesActivityWithDbSystem(
        string beforeCommand)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var sampler = new TestSampler
        {
            SamplingAction = _ => new SamplingResult(SamplingDecision.Drop),
        };
        using (Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .SetSampler(sampler)
            .Build())
        {
            this.fakeSqlClientDiagnosticSource.Write(beforeCommand, new { });
        }

        VerifySamplingParameters(sampler.LatestSamplingParameters);
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrue()
    {
        var activities = this.RunCommandWithFilter(
            cmd =>
            {
                cmd.CommandText = "select 2";
            },
            cmd =>
            {
                if (cmd is SqlCommand command)
                {
                    return command.CommandText == "select 2";
                }

                return true;
            });

        Assert.Single(activities);
        Assert.True(activities[0].IsAllDataRequested);
        Assert.True(activities[0].ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [Fact]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalse()
    {
        var activities = this.RunCommandWithFilter(
            cmd =>
            {
                cmd.CommandText = "select 1";
            },
            cmd =>
            {
                if (cmd is SqlCommand command)
                {
                    return command.CommandText == "select 2";
                }

                return true;
            });

        Assert.Empty(activities);
    }

    [Fact]
    public void ShouldNotCollectTelemetryAndShouldNotPropagateExceptionWhenFilterThrowsException()
    {
        var activities = this.RunCommandWithFilter(
            cmd =>
            {
                cmd.CommandText = "select 1";
            },
            cmd => throw new InvalidOperationException("foobar"));

        Assert.Empty(activities);
    }
#endif

    internal static void VerifyActivityData(
        CommandType commandType,
        string commandText,
        bool captureStoredProcedureCommandName,
        bool captureTextCommandContent,
        bool isFailure,
        bool recordException,
        bool shouldEnrich,
        Activity activity,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        Assert.Equal("master", activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            var status = activity.GetStatus();
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

        if (shouldEnrich)
        {
            Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enriched"));
            Assert.Equal("yes", activity.Tags.Where(tag => tag.Key == "enriched").FirstOrDefault().Value);
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enriched");
        }

        Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystem));

        if (emitOldAttributes)
        {
            Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbName));
        }

        if (emitNewAttributes)
        {
            Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        }

        switch (commandType)
        {
            case CommandType.StoredProcedure:
                if (captureStoredProcedureCommandName)
                {
                    if (emitOldAttributes)
                    {
                        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    }

                    if (emitNewAttributes)
                    {
                        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
                    }
                }
                else
                {
                    Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
                }

                break;

            case CommandType.Text:
                if (captureTextCommandContent)
                {
                    if (emitOldAttributes)
                    {
                        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    }

                    if (emitNewAttributes)
                    {
                        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
                    }
                }
                else
                {
                    Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
                }

                break;
        }
    }

    internal static void VerifySamplingParameters(SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                   && kvp.Value != null
                   && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName);
    }

    internal static void ActivityEnrichment(Activity activity, string method, object obj)
    {
        activity.SetTag("enriched", "yes");

        switch (method)
        {
            case "OnCustom":
                Assert.True(obj is SqlCommand);
                break;

            default:
                break;
        }
    }

#if !NETFRAMEWORK
    private Activity[] RunCommandWithFilter(
        Action<SqlCommand> sqlCommandSetup,
        Func<object, bool> filter)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
           .AddSqlClientInstrumentation(
               options =>
               {
                   options.Filter = filter;
               })
           .AddInMemoryExporter(activities)
           .Build())
        {
            var operationId = Guid.NewGuid();
            sqlCommandSetup(sqlCommand);

            var beforeExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = (long?)1000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                beforeExecuteEventData);

            var afterExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = 2000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
                afterExecuteEventData);
        }

        return activities.ToArray();
    }
#endif

    private class FakeSqlClientDiagnosticSource : IDisposable
    {
        private readonly DiagnosticListener listener;

        public FakeSqlClientDiagnosticSource()
        {
            this.listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);
        }

        public void Write(string name, object value)
        {
            if (this.listener.IsEnabled(name))
            {
                this.listener.Write(name, value);
            }
        }

        public void Dispose()
        {
            this.listener.Dispose();
        }
    }
}
