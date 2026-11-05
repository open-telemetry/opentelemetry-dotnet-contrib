// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
public class SqlEventSourceTests
{
    private static readonly string[] ExpectedQuerySummaries = ["select first_table", "select second_table"];

    public static TheoryData<Type, CommandType, string, bool, int, bool, bool> EventSourceFakeTestCases()
    {
        /* netfx driver can't capture queries, only stored procedure names */
        /* always emit some attribute */
        var bools = new[] { true, false };
        var query =
            from eventSourceType in new[] { typeof(FakeBehavingAdoNetSqlEventSource), typeof(FakeBehavingMdsSqlEventSource) }
            from commandType in new[] { CommandType.StoredProcedure, CommandType.Text }
            from isFailure in bools
            from tracingEnabled in bools
            from metricsEnabled in bools
            where !(commandType == CommandType.Text)
            let commandText = commandType == CommandType.Text
                ? (!isFailure ? "select 1/1" : "select 1/0")
                : "sp_who"
            let sqlExceptionNumber = 0
            select new
            {
                eventSourceType,
                commandType,
                commandText,
                isFailure,
                sqlExceptionNumber,
                tracingEnabled,
                metricsEnabled,
            };

        var testCases = new TheoryData<Type, CommandType, string, bool, int, bool, bool>();

        foreach (var item in query)
        {
            testCases.Add(
                item.eventSourceType,
                item.commandType,
                item.commandText,
                item.isFailure,
                item.sqlExceptionNumber,
                item.tracingEnabled,
                item.metricsEnabled);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(EventSourceFakeTestCases))]
    public void EventSourceFakeTests(
        Type eventSourceType,
        CommandType commandType,
        string commandText,
        bool isFailure,
        int sqlExceptionNumber,
        bool tracingEnabled,
        bool metricsEnabled)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var traceProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (tracingEnabled)
        {
            traceProviderBuilder.AddInMemoryExporter(activities)
                .AddSqlClientInstrumentation();
        }

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();

        if (metricsEnabled)
        {
            meterProviderBuilder.AddInMemoryExporter(metrics)
                .AddSqlClientInstrumentation();
        }

        using var traceProvider = traceProviderBuilder.Build();
        using var meterProvider = meterProviderBuilder.Build();

        var objectId = Guid.NewGuid().GetHashCode();
        var dataSource = "127.0.0.1\\instanceName,port";

        try
        {
            fakeSqlEventSource.WriteBeginExecuteEvent(objectId, dataSource, "master", commandType == CommandType.StoredProcedure ? commandText : string.Empty);

            // success is stored in the first bit in compositeState 0b001
            var successFlag = !isFailure ? 1 : 0;

            // isSqlException is stored in the second bit in compositeState 0b010
            var isSqlExceptionFlag = sqlExceptionNumber > 0 ? 2 : 0;

            // synchronous state is stored in the third bit in compositeState 0b100
            var synchronousFlag = false ? 4 : 0;

            var compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

            fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState, sqlExceptionNumber);
        }
        finally
        {
            traceProvider.Dispose();
            Assert.True(meterProvider.ForceFlush());
        }

        Activity? activity = null;

        if (tracingEnabled)
        {
            activity = Assert.Single(activities);
            VerifyActivityData(commandText, isFailure, dataSource, activity);
        }

        var dbClientOperationDurationMetrics = metrics
            .Where(metric => metric.Name == "db.client.operation.duration")
            .ToArray();

        if (metricsEnabled)
        {
            var metric = Assert.Single(dbClientOperationDurationMetrics);
            Assert.NotNull(metric);
            Assert.Equal("s", metric.Unit);
            Assert.Equal(MetricType.Histogram, metric.MetricType);

            var metricPoints = new List<MetricPoint>();
            foreach (var p in metric.GetMetricPoints())
            {
                metricPoints.Add(p);
            }

            var metricPoint = Assert.Single(metricPoints);

            if (activity != null)
            {
                var count = metricPoint.GetHistogramCount();
                var sum = metricPoint.GetHistogramSum();
                Assert.Equal(activity.Duration.TotalSeconds, sum);
            }
        }
        else
        {
            Assert.Empty(dbClientOperationDurationMetrics);
        }
    }

    [Theory]
    [InlineData(typeof(FakeMisbehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeMisbehavingMdsSqlEventSource))]
    public void EventSourceFakeUnknownEventWithNullPayloadTest(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeMisbehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        fakeSqlEventSource.WriteUnknownEventWithNullPayload();

        shutdownSignal.Dispose();

        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(typeof(FakeMisbehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeMisbehavingMdsSqlEventSource))]
    public void EventSourceFakeInvalidPayloadTest(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeMisbehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        fakeSqlEventSource.WriteBeginExecuteEvent("arg1");

        fakeSqlEventSource.WriteEndExecuteEvent("arg1", "arg2", "arg3", "arg4");
        shutdownSignal.Dispose();

        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void InterleavedEventSourceCommandsUseObjectIdForMetricCorrelation(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var metrics = new List<Metric>();
        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new TestSampler
            {
                SamplingAction = _ => new SamplingResult(SamplingDecision.Drop),
            })
            .AddSqlClientInstrumentation()
            .Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        var dataSource = "127.0.0.1\\instanceName,port";
        fakeSqlEventSource.WriteBeginExecuteEvent(1, dataSource, "master", "select * from first_table");
        fakeSqlEventSource.WriteBeginExecuteEvent(2, dataSource, "master", "select * from second_table");
        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);
        fakeSqlEventSource.WriteEndExecuteEvent(2, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(meterProvider.ForceFlush());

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        var querySummaries = new List<string>();
        foreach (var metricPoint in metric.GetMetricPoints())
        {
            foreach (var tag in metricPoint.Tags)
            {
                if (tag.Key == SemanticConventions.AttributeDbQuerySummary && tag.Value is string querySummary)
                {
                    querySummaries.Add(querySummary);
                }
            }
        }

        Assert.Equal(2, querySummaries.Count);
        Assert.Contains("select first_table", querySummaries);
        Assert.Contains("select second_table", querySummaries);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public async Task CallerActivityIsRestoredWhenCommandEndsOnAnotherExecutionContext(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        Assert.Null(Activity.Current);

        const int Commands = 3;

        for (var i = 0; i < Commands; i++)
        {
            var objectId = i + 1;

            // The event which ends an asynchronous command may be written on a copy of the execution context
            // of the caller, so the activity cannot restore the caller's current activity when it stops.
            fakeSqlEventSource.WriteBeginExecuteEvent(objectId, "127.0.0.1", "master", "select 1");

            Assert.Null(Activity.Current);

            await Task.Run(() => fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState: 1, sqlExceptionNumber: 0), TestContext.Current.CancellationToken);

            Assert.Null(Activity.Current);
        }

        Assert.True(tracerProvider.ForceFlush());

        Assert.Equal(Commands, activities.Count);
        Assert.All(activities, x => Assert.True(x.IsStopped));
        Assert.All(activities, x => Assert.Equal(default, x.ParentSpanId));
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void CurrentActivityOfEndIsNotChangedWhenCommandEnds(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        using var caller = new Activity("caller").SetIdFormat(ActivityIdFormat.W3C).Start();
        caller.ActivityTraceFlags = ActivityTraceFlags.Recorded;

        fakeSqlEventSource.WriteBeginExecuteEvent(1, "127.0.0.1", "master", "select 1");

        // The command ends on an execution context which has a different current activity to the caller.
        using var other = new Activity("other").SetIdFormat(ActivityIdFormat.W3C).Start();

        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.Equal(other, Activity.Current);

        Assert.True(tracerProvider.ForceFlush());

        var activity = Assert.Single(activities);
        Assert.True(activity.IsStopped);
        Assert.Equal(caller.SpanId, activity.ParentSpanId);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void ActivityOfCommandWhichNeverEndedIsNotUsedWhenItIsExecutedWhileInstrumentationIsDisabled(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        using (Sdk.CreateTracerProviderBuilder().AddSqlClientInstrumentation().Build())
        {
            // The first execution never ends.
            fakeSqlEventSource.WriteBeginExecuteEvent(1, "127.0.0.1", "master", "select 1");
        }

        // The command is executed again while the instrumentation is disabled.
        fakeSqlEventSource.WriteBeginExecuteEvent(1, "127.0.0.1", "master", "select 1");

        // The instrumentation is enabled again before the command ends, under a parent which is not sampled.
        var activities = new List<Activity>();
        using var parent = new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(tracerProvider.ForceFlush());

        Assert.Empty(activities);
        Assert.Equal(parent, Activity.Current);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void PendingCommandLimitDropsOldExecutionsAndRecordsNewOnes(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var activities = new List<Activity>();
        var metrics = new List<Metric>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        // An unmatched begin may belong to an abandoned command or one which is still executing.
        // Exceed the pending-command limit before either the oldest or newest command completes.
        var last = SqlEventSourceListener.MaxPendingCommands * 2;

        for (var objectId = 1; objectId <= last; objectId++)
        {
            fakeSqlEventSource.WriteBeginExecuteEvent(objectId, "127.0.0.1", "master", "select 1");
        }

        // The oldest command was dropped during a capacity reset, even though it eventually completes.
        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(tracerProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());
        Assert.Empty(activities);
        Assert.Empty(metrics);

        fakeSqlEventSource.WriteEndExecuteEvent(last, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(tracerProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());
        Assert.True(Assert.Single(activities).IsStopped);

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        var metricPoints = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            metricPoints.Add(point);
        }

        Assert.Equal(1, Assert.Single(metricPoints).GetHistogramCount());
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void CommandReusedAfterCapacityResetKeepsItsNewActivity(Type eventSourceType)
    {
        using var eventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        // Fill the pending-command cache and force its capacity reset before reusing an id.
        for (var objectId = 1; objectId <= SqlEventSourceListener.MaxPendingCommands + 1; objectId++)
        {
            eventSource.WriteBeginExecuteEvent(objectId, "127.0.0.1", "master", "select * from abandoned_command");
        }

        eventSource.WriteBeginExecuteEvent(1, "127.0.0.1", "master", "select * from reused_command");
        eventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(tracerProvider.ForceFlush());
        var activity = Assert.Single(activities);
        Assert.True(activity.IsStopped);
        Assert.Equal("select reused_command", activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary));
    }

    [Fact]
    public void InterleavedEventSourceCommandsFromDifferentProvidersUseEventSourceAndObjectId()
    {
        var metrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        using var adoNetSqlEventSource = new FakeCrossProviderAdoNetSqlEventSource();
        using var mdsSqlEventSource = new FakeCrossProviderMdsSqlEventSource();

        const int objectId = 1;
        var dataSource = "127.0.0.1\\instanceName,port";
        adoNetSqlEventSource.WriteBeginExecuteEvent(objectId, dataSource, "master", "select * from first_table");
        mdsSqlEventSource.WriteBeginExecuteEvent(objectId, dataSource, "master", "select * from second_table");
        mdsSqlEventSource.WriteEndExecuteEvent(objectId, compositeState: 1, sqlExceptionNumber: 0);
        adoNetSqlEventSource.WriteEndExecuteEvent(objectId, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(meterProvider.ForceFlush());

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        var querySummaries = new List<string>();
        foreach (var metricPoint in metric.GetMetricPoints())
        {
            foreach (var tag in metricPoint.Tags)
            {
                if (tag.Key == SemanticConventions.AttributeDbQuerySummary && tag.Value is string querySummary)
                {
                    querySummaries.Add(querySummary);
                }
            }
        }

        Assert.Equal(ExpectedQuerySummaries, querySummaries.OrderBy(value => value));
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void EventSourceBeginWithEmptyCommandTextClearsPreviousQuerySummary(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var metrics = new List<Metric>();
        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new TestSampler
            {
                SamplingAction = _ => new SamplingResult(SamplingDecision.Drop),
            })
            .AddSqlClientInstrumentation()
            .Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        var dataSource = "127.0.0.1\\instanceName,port";
        fakeSqlEventSource.WriteBeginExecuteEvent(1, dataSource, "master", "select * from first_table");
        fakeSqlEventSource.WriteBeginExecuteEvent(1, dataSource, "master", string.Empty);
        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(meterProvider.ForceFlush());

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        foreach (var metricPoint in metric.GetMetricPoints())
        {
            foreach (var tag in metricPoint.Tags)
            {
                Assert.NotEqual(SemanticConventions.AttributeDbQuerySummary, tag.Key);
            }
        }
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void EventSourceEndWithoutBeginDoesNotRecordMetric(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var metrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(meterProvider.ForceFlush());
        Assert.DoesNotContain(metrics, metric => metric.Name == "db.client.operation.duration");
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void EventSourceMetricsOnlyRecordsDurationWhenActivityIsSampledByAnotherListener(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var sourceName = typeof(SqlTelemetryHelper).Assembly.GetName().Name;

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ActivityStarted = activities.Add,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ShouldListenTo = source => source.Name == sourceName,
        };

        ActivitySource.AddActivityListener(listener);

        var metrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        var dataSource = "127.0.0.1\\instanceName,port";
        var stopwatch = Stopwatch.StartNew();

        fakeSqlEventSource.WriteBeginExecuteEvent(1, dataSource, "master", "select * from first_table");
        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        stopwatch.Stop();

        Assert.True(meterProvider.ForceFlush());

        var activity = Assert.Single(activities);
        Assert.True(activity.IsStopped);

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");

        var metricPoints = new List<MetricPoint>();
        foreach (var metricPoint in metric.GetMetricPoints())
        {
            metricPoints.Add(metricPoint);
        }

        var point = Assert.Single(metricPoints);
        Assert.Equal(1, point.GetHistogramCount());
        Assert.InRange(point.GetHistogramSum(), 0, stopwatch.Elapsed.TotalSeconds);

        string? querySummary = null;
        foreach (var tag in point.Tags)
        {
            if (tag.Key == SemanticConventions.AttributeDbQuerySummary)
            {
                querySummary = tag.Value as string;
            }
        }

        Assert.Equal("select first_table", querySummary);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void EventSourceBeginWithoutEndIsNotTrackedIndefinitely(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);
        using var listener = new SqlEventSourceListener();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .Build();

        // SqlClient writes no EndExecute for an asynchronous command which fails before it is sent.
        var dataSource = "127.0.0.1\\instanceName,port";
        var lastObjectId = 2 * SqlEventSourceListener.MaxPendingCommands;
        for (var objectId = 1; objectId <= lastObjectId; objectId++)
        {
            fakeSqlEventSource.WriteBeginExecuteEvent(objectId, dataSource, "master", "select * from first_table");
        }

        var pendingCommandCount = listener.PendingCommandCount;
        Assert.InRange(pendingCommandCount, 1, SqlEventSourceListener.MaxPendingCommands);

        // The command which began last is still tracked.
        fakeSqlEventSource.WriteEndExecuteEvent(lastObjectId, compositeState: 1, sqlExceptionNumber: 0);

        Assert.Equal(pendingCommandCount - 1, listener.PendingCommandCount);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void EventSourceBeginWithoutEndIsBoundedWhenWrittenConcurrently(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);
        using var listener = new SqlEventSourceListener();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .Build();

        var dataSource = "127.0.0.1\\instanceName,port";
        var threads = 4;
        var commandsPerThread = SqlEventSourceListener.MaxPendingCommands;

        // Each thread tracks enough commands to roll the limit over several times, some of which end.
        Parallel.For(
            0,
            threads,
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            thread =>
            {
                for (var i = 0; i < commandsPerThread; i++)
                {
                    var objectId = (thread * commandsPerThread) + i + 1;
                    fakeSqlEventSource.WriteBeginExecuteEvent(objectId, dataSource, "master", "select * from first_table");

                    if (i % 2 == 0)
                    {
                        fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState: 1, sqlExceptionNumber: 0);
                    }
                }
            });

        Assert.InRange(listener.PendingCommandCount, 0, SqlEventSourceListener.MaxPendingCommands);

        // Ending every command leaves nothing tracked, so no entry was left behind uncounted.
        for (var objectId = 1; objectId <= threads * commandsPerThread; objectId++)
        {
            fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState: 1, sqlExceptionNumber: 0);
        }

        Assert.Equal(0, listener.PendingCommandCount);
    }

    [Theory]
    [InlineData(typeof(FakeOtherKeywordAdoNetSqlEventSource))]
    [InlineData(typeof(FakeOtherKeywordMdsSqlEventSource))]
    public void EventSourceEventsWithOtherKeywordsAreNotProcessed(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        var dataSource = "127.0.0.1\\instanceName,port";
        fakeSqlEventSource.WriteBeginExecuteEvent(1, dataSource, "master", "select * from first_table");
        fakeSqlEventSource.WriteEndExecuteEvent(1, compositeState: 1, sqlExceptionNumber: 0);

        Assert.True(traceProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());

        Assert.Empty(activities);
        Assert.DoesNotContain(metrics, metric => metric.Name == "db.client.operation.duration");
    }

    private static void VerifyActivityData(
        string commandText,
        bool isFailure,
        string dataSource,
        Activity activity)
    {
        Assert.Equal("instanceName.master", activity.DisplayName);

        Assert.Equal(ActivityKind.Client, activity.Kind);

        var connectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

        if (!string.IsNullOrEmpty(connectionDetails.ServerHostName))
        {
            Assert.Equal(connectionDetails.ServerHostName, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }
        else
        {
            Assert.Equal(connectionDetails.ServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }

        if (connectionDetails.BoxedPort != null)
        {
            Assert.Equal(connectionDetails.BoxedPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        }

        Assert.Equal(SqlTelemetryHelper.MicrosoftSqlServerDbSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystemName));
        Assert.Equal("instanceName.master", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);
        }
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    // Helper interface to be able to have single test method for multiple EventSources, want to keep it close to the event sources themselves.
    private interface IFakeBehavingSqlEventSource : IDisposable
#pragma warning restore SA1201 // Elements should appear in the correct order
    {
        void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText);

        void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber);
    }

    private interface IFakeMisbehavingSqlEventSource : IDisposable
    {
        void WriteBeginExecuteEvent(string arg1);

        void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4);

        void WriteUnknownEventWithNullPayload();
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeFriendly")]
    private class FakeBehavingAdoNetSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);

        public static class Keywords
        {
            public const EventKeywords ExecutionTrace = SqlEventSourceListener.ExecuteEventKeywords;
        }
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeCrossProviderAdoNet")]
    private class FakeCrossProviderAdoNetSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);

        public static class Keywords
        {
            public const EventKeywords ExecutionTrace = SqlEventSourceListener.ExecuteEventKeywords;
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeFriendly")]
    private class FakeBehavingMdsSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);

        public static class Keywords
        {
            public const EventKeywords ExecutionTrace = SqlEventSourceListener.ExecuteEventKeywords;
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeCrossProviderMds")]
    private class FakeCrossProviderMdsSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);

        public static class Keywords
        {
            public const EventKeywords ExecutionTrace = SqlEventSourceListener.ExecuteEventKeywords;
        }
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeOtherKeyword")]
    private class FakeOtherKeywordAdoNetSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.Other)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.Other)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);

        public static class Keywords
        {
            public const EventKeywords Other = (EventKeywords)2;
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeOtherKeyword")]
    private class FakeOtherKeywordMdsSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.Other)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.Other)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);

        public static class Keywords
        {
            public const EventKeywords Other = (EventKeywords)2;
        }
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeEvil")]
    private class FakeMisbehavingAdoNetSqlEventSource : EventSource, IFakeMisbehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteBeginExecuteEvent(string arg1)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, arg1);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, arg1, arg2, arg3, arg4);

        [Event(3)]
        public void WriteUnknownEventWithNullPayload()
        {
            object[]? args = null;

            this.WriteEvent(3, args);
        }

        public static class Keywords
        {
            public const EventKeywords ExecutionTrace = SqlEventSourceListener.ExecuteEventKeywords;
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeEvil")]
    private class FakeMisbehavingMdsSqlEventSource : EventSource, IFakeMisbehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteBeginExecuteEvent(string arg1)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, arg1);

        [Event(SqlEventSourceListener.EndExecuteEventId, Keywords = Keywords.ExecutionTrace)]
        public void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, arg1, arg2, arg3, arg4);

        [Event(3)]
        public void WriteUnknownEventWithNullPayload()
        {
            object[]? args = null;

            this.WriteEvent(3, args);
        }

        public static class Keywords
        {
            public const EventKeywords ExecutionTrace = SqlEventSourceListener.ExecuteEventKeywords;
        }
    }
}
#endif
