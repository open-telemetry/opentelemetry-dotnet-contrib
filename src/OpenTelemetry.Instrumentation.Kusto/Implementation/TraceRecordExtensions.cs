// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Classifies a Kusto client <see cref="global::Kusto.Cloud.Platform.Utils.TraceRecord"/> by the markers the
/// client writes into its trace output: RestClient2 writes "$$HTTPREQUEST[", the activity layer writes
/// "MonitoredActivityCompleted", and exceptions are traced under the "EXC_CTOR" source id. There is no public
/// reference to link, so they are matched by string.
/// </summary>
internal static class TraceRecordExtensions
{
    public static bool IsRequestStart(this KustoUtils.TraceRecord record)
    {
        return record.Message.StartsWith("$$HTTPREQUEST[", StringComparison.Ordinal);
    }

    public static bool IsException(this KustoUtils.TraceRecord record)
    {
        return record.SourceId == "EXC_CTOR";
    }

    public static bool IsActivityComplete(this KustoUtils.TraceRecord record)
    {
        return record.Message.StartsWith("MonitoredActivityCompleted", StringComparison.Ordinal);
    }
}
