// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation;

#if NET6_0_OR_GREATER
internal class SqlClientMetricDiagnosticListener : ListenerHandler
{
    internal static readonly Assembly Assembly = typeof(SqlClientMetricDiagnosticListener).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string MeterName = AssemblyName.Name!;
    internal static readonly string MeterVersion = AssemblyName.Version!.ToString();
    internal static readonly Meter Meter = new(MeterName, MeterVersion);
    private static readonly Histogram<double> DbClientOperationDuration = Meter.CreateHistogram<double>(
        "db.client.operation.duration",
        "s",
        "Duration of database client operations.");

#if !NET7_0_OR_GREATER
    private static readonly double StopwatchTickFrequency = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

    private readonly AsyncLocal<long> beginTimestamp = new AsyncLocal<long>();

    public SqlClientMetricDiagnosticListener(string sourceName)
        : base(sourceName)
    {
    }

    public override void OnEventWritten(string name, object? payload)
    {
        var activity = Activity.Current;
        switch (name)
        {
            case SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand:
            case SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand:
                {
                    // only record the start time if tracing is disabled or sampling has occured

                    if (activity == null)
                    {
                        this.beginTimestamp.Value = Stopwatch.GetTimestamp();
                    }

                    break;
                }

            case SqlClientDiagnosticListener.SqlDataAfterExecuteCommand:
            case SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand:
                {
                    TagList tags = default;
                    tags.Add(new("db.system", "mssql"));

                    if (activity == null)
                    {
                        if (this.beginTimestamp.Value != 0)
                        {
                            var endTimestamp = Stopwatch.GetTimestamp();
                            var duration = GetElapsedTime(this.beginTimestamp.Value, endTimestamp);
                            DbClientOperationDuration.Record(duration.TotalSeconds, tags);
                        }
                    }
                    else
                    {
                        activity.Stop();
                        DbClientOperationDuration.Record(activity.Duration.TotalSeconds, tags);
                    }

                    break;
                }
        }
    }

    private static TimeSpan GetElapsedTime(long beginTimestamp, long endingTimestamp)
    {
#if !NET7_0_OR_GREATER
        var diff = endingTimestamp - beginTimestamp;
        return new TimeSpan((long)(diff * StopwatchTickFrequency));
#else
        return Stopwatch.GetElapsedTime(beginTimestamp, endingTimestamp);
#endif
    }
}
#endif
