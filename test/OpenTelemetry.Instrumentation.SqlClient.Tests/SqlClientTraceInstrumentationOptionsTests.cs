// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class SqlClientTraceInstrumentationOptionsTests
{
    static SqlClientTraceInstrumentationOptionsTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);
    }

    [Theory]
    [InlineData("localhost", "localhost", null, null, null)]
    [InlineData("127.0.0.1", null, "127.0.0.1", null, null)]
    [InlineData("127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData("127.0.0.1, 1818", null, "127.0.0.1", null, "1818")]
    [InlineData("127.0.0.1  \\  instanceName", null, "127.0.0.1", "instanceName", null)]
    [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
    [InlineData("tcp:127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
    [InlineData("tcp:localhost", "localhost", null, null, null)]
    [InlineData("tcp : localhost", "localhost", null, null, null)]
    [InlineData("np : localhost", "localhost", null, null, null)]
    [InlineData("lpc:localhost", "localhost", null, null, null)]
    [InlineData("np:\\\\localhost\\pipe\\sql\\query", "localhost", null, null, null)]
    [InlineData("np : \\\\localhost\\pipe\\sql\\query", "localhost", null, null, null)]
    [InlineData("np:\\\\localhost\\pipe\\MSSQL$instanceName\\sql\\query", "localhost", null, "instanceName", null)]
    public void ParseDataSourceTests(
        string dataSource,
        string? expectedServerHostName,
        string? expectedServerIpAddress,
        string? expectedInstanceName,
        string? expectedPort)
    {
        var sqlConnectionDetails = SqlClientTraceInstrumentationOptions.ParseDataSource(dataSource);

        Assert.NotNull(sqlConnectionDetails);
        Assert.Equal(expectedServerHostName, sqlConnectionDetails.ServerHostName);
        Assert.Equal(expectedServerIpAddress, sqlConnectionDetails.ServerIpAddress);
        Assert.Equal(expectedInstanceName, sqlConnectionDetails.InstanceName);
        Assert.Equal(expectedPort, sqlConnectionDetails.Port);
    }

    [Theory]
    [InlineData(true, "localhost", "localhost", null, null, null)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, "1434")]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
    [InlineData(false, "localhost", null, null, null, null)]

    // Test cases when EmitOldAttributes = false and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database)
    [InlineData(true, "localhost", "localhost", null, null, null, false, true)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null, false, true)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, "1434", false, true)]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", null, "1818", false, true)]
    [InlineData(false, "localhost", null, null, null, null, false, true)]

    // Test cases when EmitOldAttributes = true and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database/dup)
    [InlineData(true, "localhost", "localhost", null, null, null, true, true)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null, true, true)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, "1434", true, true)]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818", true, true)]
    [InlineData(false, "localhost", null, null, null, null, true, true)]
    public void SqlClientTraceInstrumentationOptions_EnableConnectionLevelAttributes(
        bool enableConnectionLevelAttributes,
        string dataSource,
        string? expectedServerHostName,
        string? expectedServerIpAddress,
        string? expectedInstanceName,
        string? expectedPort,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        var source = new ActivitySource("sql-client-instrumentation");
        var activity = source.StartActivity("Test Sql Activity");
        Assert.NotNull(activity);
        var options = new SqlClientTraceInstrumentationOptions()
        {
            EnableConnectionLevelAttributes = enableConnectionLevelAttributes,
            EmitOldAttributes = emitOldAttributes,
            EmitNewAttributes = emitNewAttributes,
        };
        options.AddConnectionLevelDetailsToActivity(dataSource, activity);

        Assert.Equal(expectedServerHostName ?? expectedServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(emitOldAttributes ? expectedInstanceName : null, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));
    }

    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsDatabaseDup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName] = "database/dup" })
            .Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.True(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsNotSpecified()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.True(options.EmitOldAttributes);
        Assert.False(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitNewAttributesWhenStabilityOptInIsDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName] = "database" })
            .Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.False(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }
}
