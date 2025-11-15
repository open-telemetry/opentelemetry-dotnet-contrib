// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class TraceRecordExtensions
{
    public static bool IsRequestStart(this KustoUtils.TraceRecord record)
    {
        return record.Message.StartsWith("$$HTTPREQUEST[", StringComparison.Ordinal);
    }

    public static bool IsException(this KustoUtils.TraceRecord record)
    {
        return record.TraceSourceName == "KD.Exceptions";
    }

    public static bool IsActivityComplete(this KustoUtils.TraceRecord record)
    {
        return record.Message.StartsWith("MonitoredActivityCompleted", StringComparison.Ordinal);
    }
}
