// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
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
    [InlineData(CommandType.Text, "select 1/1", true)]
    [InlineData(CommandType.Text, "select 1/0", false, true)]
    [InlineData(CommandType.Text, "select 1/0", false, true, false, false)]
    [InlineData(CommandType.Text, "select 1/0", false, true, true, false)]
    [InlineData(CommandType.StoredProcedure, "sp_who")]
    public void SuccessfulCommandTest(
        CommandType commandType,
        string commandText,
        bool captureTextCommandContent = false,
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

        SqlClientTests.VerifyActivityData(commandType, commandText, captureTextCommandContent, isFailure, recordException, shouldEnrich, activity);
        SqlClientTests.VerifySamplingParameters(sampler.LatestSamplingParameters);

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
