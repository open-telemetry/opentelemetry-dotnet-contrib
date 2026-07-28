// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Classifies a Kusto client <see cref="KustoUtils.TraceRecord"/> by the markers the
/// client writes into its trace output: RestClient2 writes "$$HTTPREQUEST[", the activity layer writes
/// "MonitoredActivityCompleted", and exceptions are traced under a source that is "EXC_CTOR" or begins with
/// "EXC_CTOR.". There is no public reference to link, so they are matched by string.
/// </summary>
internal static class TraceRecordExtensions
{
    public static bool IsRequestStart(this KustoUtils.TraceRecord record)
    {
        return record.Message.StartsWith("$$HTTPREQUEST[", StringComparison.Ordinal);
    }

    public static bool IsException(this KustoUtils.TraceRecord record)
    {
        var sourceId = record.SourceId;
        return sourceId == "EXC_CTOR"
            || sourceId?.StartsWith("EXC_CTOR.", StringComparison.Ordinal) == true;
    }

    public static bool IsActivityComplete(this KustoUtils.TraceRecord record)
    {
        return record.Message.StartsWith("MonitoredActivityCompleted", StringComparison.Ordinal);
    }
}
