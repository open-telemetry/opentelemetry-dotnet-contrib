// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Testcontainers.MsSql;
using Testcontainers.SqlEdge;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Trait("CategoryName", "SqlIntegrationTests")]
public sealed class SqlClientIntegrationTests : IClassFixture<SqlClientIntegrationTestsFixture>
{
    private readonly SqlClientIntegrationTestsFixture fixture;

    public SqlClientIntegrationTests(SqlClientIntegrationTestsFixture fixture)
    {
        this.fixture = fixture;
    }

    [EnabledOnDockerPlatformTheory(EnabledOnDockerPlatformTheoryAttribute.DockerPlatform.Linux)]
    [InlineData(CommandType.Text, "select 1/1")]
    [InlineData(CommandType.Text, "select 1/1", true, "select ?/?")]
    [InlineData(CommandType.Text, "select 1/0", false, null, true)]
    [InlineData(CommandType.Text, "select 1/0", false, null, true, false, false)]
    [InlineData(CommandType.Text, "select 1/0", false, null, true, true, false)]
    [InlineData(CommandType.StoredProcedure, "sp_who", false, "sp_who")]
    public void SuccessfulCommandTest(
        CommandType commandType,
        string commandText,
        bool captureTextCommandContent = false,
        string? sanitizedCommandText = null,
        bool isFailure = false,
        bool recordException = false,
        bool shouldEnrich = true)
    {
#if NETFRAMEWORK
        // Disable things not available on netfx
        recordException = false;
        shouldEnrich = false;
#endif

        var sampler = new TestSampler();
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddInMemoryExporter(activities)
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureTextCommandContent;
                options.RecordException = recordException;
                if (shouldEnrich)
                {
                    options.Enrich = SqlClientTests.ActivityEnrichment;
                }
            })
            .Build();

        using var sqlConnection = new SqlConnection(this.GetConnectionString());

        sqlConnection.Open();

        var dataSource = sqlConnection.DataSource;

        sqlConnection.ChangeDatabase("master");
#pragma warning disable CA2100
        using var sqlCommand = new SqlCommand(commandText, sqlConnection)
#pragma warning restore CA2100
        {
            CommandType = commandType,
        };

        try
        {
            sqlCommand.ExecuteNonQuery();
        }
        catch
        {
        }

        Assert.Single(activities);
        var activity = activities[0];

        VerifyActivityData(commandType, sanitizedCommandText, captureTextCommandContent, isFailure, recordException, shouldEnrich, activity);
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
    }

    private static void VerifyActivityData(
        CommandType commandType,
        string? commandText,
        bool captureTextCommandContent,
        bool isFailure,
        bool recordException,
        bool shouldEnrich,
        Activity activity,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        if (emitNewAttributes)
        {
            Assert.Equal("MSSQLLocalDB.master", activity.DisplayName);
        }
        else
        {
            Assert.Equal("master", activity.DisplayName);
        }

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

        if (shouldEnrich)
        {
            Assert.Contains(activity.Tags, tag => tag.Key == "enriched");
            Assert.Equal("yes", activity.Tags.FirstOrDefault(tag => tag.Key == "enriched").Value);
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enriched");
        }

        if (emitOldAttributes)
        {
            Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDbSystem, activity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbName));
        }

        if (emitNewAttributes)
        {
            Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDbSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystemName));
            Assert.Equal("MSSQLLocalDB.master", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        }

        switch (commandType)
        {
            case CommandType.StoredProcedure:
                if (emitOldAttributes)
                {
                    Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                }

                if (emitNewAttributes)
                {
                    Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStoredProcedureName));
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
            kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                   && kvp.Value != null
                   && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDbSystem);
    }

    private string GetConnectionString()
    {
        return this.fixture.DatabaseContainer switch
        {
            SqlEdgeContainer container => container.GetConnectionString(),
            MsSqlContainer container => container.GetConnectionString(),
            _ => throw new InvalidOperationException($"Container type '${this.fixture.DatabaseContainer.GetType().Name}' is not supported."),
        };
    }
}
