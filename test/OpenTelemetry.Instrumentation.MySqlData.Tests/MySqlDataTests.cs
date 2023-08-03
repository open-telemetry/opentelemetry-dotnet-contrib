// <copyright file="MySqlDataTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using Moq;
using MySql.Data.MySqlClient;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.MySqlData.Tests;

public class MySqlDataTests
{
    private const string ConnStr = "database=mysql;server=127.0.0.1;user id=root;password=123456;port=3306;pooling=False";

    [Theory]
    [InlineData("select 1/1", true, true, true, false)]
    [InlineData("select 1/1", true, true, false, false)]
    [InlineData("selext 1/1", true, true, true, true)]
    public void SuccessTraceEventTest(
        string commandText,
        bool setDbStatement = false,
        bool recordException = false,
        bool enableConnectionLevelAttributes = false,
        bool isFailure = false)
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        var sampler = new TestSampler();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .SetSampler(sampler)
            .AddMySqlDataInstrumentation(options =>
            {
                options.SetDbStatement = setDbStatement;
                options.RecordException = recordException;
                options.EnableConnectionLevelAttributes = enableConnectionLevelAttributes;
            })
            .Build();

        var traceListener = (TraceListener)Assert.Single(MySqlTrace.Listeners)!;

        ExecuteSuccessQuery(traceListener, commandText, isFailure);

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        VerifyActivityData(commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure, activity);
    }

    [Fact]
    public void MySqlDataInstrumentationEventSource_test()
    {
        MySqlDataInstrumentationEventSource.Log.UnknownMySqlTraceEventType(15, "UnknownMySqlTraceEventType");
        MySqlDataInstrumentationEventSource.Log.ErrorTraceEvent(1, "ErrorTraceEvent", "ErrorTraceEvent exception");
    }

    [Theory]
    [InlineData(MySqlTraceEventType.ConnectionClosed)]
    [InlineData(MySqlTraceEventType.ResultOpened)]
    [InlineData(MySqlTraceEventType.ResultClosed)]
    [InlineData(MySqlTraceEventType.StatementPrepared)]
    [InlineData(MySqlTraceEventType.StatementExecuted)]
    [InlineData(MySqlTraceEventType.StatementClosed)]
    [InlineData(MySqlTraceEventType.NonQuery)]
    [InlineData(MySqlTraceEventType.UsageAdvisorWarning)]
    [InlineData(MySqlTraceEventType.Warning)]
    [InlineData(MySqlTraceEventType.QueryNormalized)]
    [InlineData((MySqlTraceEventType)0)]
    public void UnknownMySqlTraceEventType(MySqlTraceEventType eventType)
    {
        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        var sampler = new TestSampler();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .SetSampler(sampler)
            .AddMySqlDataInstrumentation()
            .Build();

        var traceListener = (TraceListener?)Assert.Single(MySqlTrace.Listeners);

        traceListener?.TraceEvent(
            new TraceEventCache(),
            "mysql",
            TraceEventType.Information,
            (int)eventType,
            "{0}: Connection Opened: connection string = '{1}'",
            1L,
            ConnStr,
            10);

        Assert.Single(activityProcessor.Invocations);
    }

    private static void VerifyActivityData(
        string commandText,
        bool setDbStatement,
        bool recordException,
        bool enableConnectionLevelAttributes,
        bool isFailure,
        Activity activity)
    {
        if (!isFailure)
        {
            Assert.Equal(Status.Unset, activity.GetStatus());
        }
        else
        {
            var status = activity.GetStatus();
            Assert.Equal(Status.Error.StatusCode, status.StatusCode);
            Assert.NotNull(status.Description);

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

        Assert.Equal("mysql", activity.GetTagValue(SemanticConventions.AttributeDbName));

        if (setDbStatement)
        {
            Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }
        else
        {
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        var dataSource = new MySqlConnectionStringBuilder(ConnStr).Server;

        if (!enableConnectionLevelAttributes)
        {
            Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
        else
        {
            var uriHostNameType = Uri.CheckHostName(dataSource);
            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
            }
            else
            {
                Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            }
        }
    }

    private static void ExecuteSuccessQuery(TraceListener listener, string query, bool isFailure)
    {
        // Connection opened
        listener.TraceEvent(
            new TraceEventCache(),
            "mysql",
            TraceEventType.Information,
            1,
            "{0}: Connection Opened: connection string = '{1}'",
            1L,
            ConnStr,
            10);

        // Query opened
        listener.TraceEvent(
            new TraceEventCache(),
            "mysql",
            TraceEventType.Information,
            3,
            "{0}: Query Opened: {2}",
            1L,
            9,
            query);

        if (isFailure)
        {
            // Query error
            listener.TraceEvent(
                new TraceEventCache(),
                "mysql",
                TraceEventType.Information,
                13,
                "{0}: Error encountered attempting to open result: Number={1}, Message={2}",
                1L,
                1064,
                "You have an error in your SQL syntax; check the manual that corresponds to your MySQL server version for the right syntax to use near 'selext 1/1' at line 1");
        }
        else
        {
            // Query closed
            listener.TraceEvent(
                new TraceEventCache(),
                "mysql",
                TraceEventType.Information,
                6,
                "{0}: Query Closed",
                1L);
        }
    }
}
