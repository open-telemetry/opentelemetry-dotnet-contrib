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
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using MySql.Data.MySqlClient;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.MySqlData.Tests;

public class MySqlDataTests
{
    private const string ConnStr =
        "database=mysql;server=127.0.0.1;user id=root;password=123456;port=3306;pooling=False";

    [Theory]
    [InlineData("select 1/1", true, true, true, false)]
    [InlineData("select 1/1", false, true, true, false)]
    [InlineData("select 1/1", true, false, true, false)]
    [InlineData("select 1/1", true, true, false, false)]
    [InlineData("select throw", true, true, true, true)]
    public async Task ExecuteCommandTest(
        string commandText,
        bool setDbStatement = false,
        bool recordException = false,
        bool enableConnectionLevelAttributes = false,
        bool isFailure = false)
    {
        await this.SuccessTraceEventTest(c => c.ExecuteNonQuery(), commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure);
        await this.SuccessTraceEventTest(c => c.ExecuteNonQueryAsync(), commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure);
        await this.SuccessTraceEventTest(c => c.ExecuteReader(), commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure);
        await this.SuccessTraceEventTest(c => c.ExecuteReaderAsync(), commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure);
        await this.SuccessTraceEventTest(c => c.ExecuteScalar(), commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure);
        await this.SuccessTraceEventTest(c => c.ExecuteScalarAsync(), commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure);
    }

    private async Task SuccessTraceEventTest<TResult>(
        Func<DbCommand, TResult> execute,
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
            .AddMySqlDataInstrumentation<MysqlCommandStub>(options =>
            {
                options.SetDbStatement = setDbStatement;
                options.RecordException = recordException;
                options.EnableConnectionLevelAttributes = enableConnectionLevelAttributes;
            })
            .Build();

        var mySqlConnection = new MySqlConnection(ConnStr);

        try
        {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            var result = execute(new MysqlCommandStub(commandText, mySqlConnection));
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            if (result is Task task)
            {
                await task;
            }
        }
        catch
        {
            // exception ignored
        }

        Assert.Equal(3, activityProcessor.Invocations.Count);

        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        this.VerifyActivityData(
            commandText,
            setDbStatement,
            recordException,
            enableConnectionLevelAttributes,
            isFailure,
            activity);
    }

    private void VerifyActivityData(
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
}
