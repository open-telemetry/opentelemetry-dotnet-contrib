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
    [InlineData(CommandType.Text, "select 1/1", false)]
    [InlineData(CommandType.Text, "select 1/1", false, true)]
    [InlineData(CommandType.Text, "select 1/0", false, false, true)]
    [InlineData(CommandType.Text, "select 1/0", false, false, true, false, false)]
    [InlineData(CommandType.Text, "select 1/0", false, false, true, true, false)]
    [InlineData(CommandType.StoredProcedure, "sp_who", false)]
    [InlineData(CommandType.StoredProcedure, "sp_who", true)]
    public void SuccessfulCommandTest(
        CommandType commandType,
        string commandText,
        bool captureStoredProcedureCommandName,
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
#if !NETFRAMEWORK
                options.SetDbStatementForStoredProcedure = captureStoredProcedureCommandName;
                options.SetDbStatementForText = captureTextCommandContent;
#else
                options.SetDbStatementForText = captureStoredProcedureCommandName || captureTextCommandContent;
#endif
                options.RecordException = recordException;
                if (shouldEnrich)
                {
                    options.Enrich = SqlClientTests.ActivityEnrichment;
                }
            })
            .Build();

        using SqlConnection sqlConnection = new SqlConnection(this.GetConnectionString());

        sqlConnection.Open();

        string dataSource = sqlConnection.DataSource;

        sqlConnection.ChangeDatabase("master");
#pragma warning disable CA2100
        using SqlCommand sqlCommand = new SqlCommand(commandText, sqlConnection)
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

        SqlClientTests.VerifyActivityData(commandType, commandText, captureStoredProcedureCommandName, captureTextCommandContent, isFailure, recordException, shouldEnrich, activity);
        SqlClientTests.VerifySamplingParameters(sampler.LatestSamplingParameters);
    }

    private string GetConnectionString()
    {
        switch (this.fixture.DatabaseContainer)
        {
            case SqlEdgeContainer container:
                return container.GetConnectionString();
            case MsSqlContainer container:
                return container.GetConnectionString();
            default:
                throw new InvalidOperationException($"Container type '${this.fixture.DatabaseContainer.GetType().Name}' is not supported.");
        }
    }
}
