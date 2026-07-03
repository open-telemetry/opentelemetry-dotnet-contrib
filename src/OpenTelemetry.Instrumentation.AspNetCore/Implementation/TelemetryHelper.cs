// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal static class TelemetryHelper
{
    public static readonly object[] BoxedStatusCodes = InitializeBoxedStatusCodes();
    internal static readonly RequestDataHelper RequestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: false);

    // Boxed values for the most common ASP.NET Core server ports to avoid boxing server.port per request.
    private static readonly object Port80 = 80;
    private static readonly object Port443 = 443;
    private static readonly object Port8080 = 8080;
    private static readonly object Port5000 = 5000;
    private static readonly object Port5001 = 5001;

    // Single-value cache for any other (e.g. dynamically-assigned) port. The server's listening
    // port is effectively constant for the lifetime of the process, so this avoids boxing on the
    // common path. The Host header is client-controlled, so the cache is intentionally limited to
    // a single entry; a miss simply boxes the value again. Reads/writes of a reference are atomic
    // and a race only causes an occasional extra allocation, so no locking is required.
    private static object? lastBoxedPort;

    public static object GetBoxedStatusCode(int statusCode) =>
        statusCode is >= 100 and < 600 ? BoxedStatusCodes[statusCode - 100] : statusCode;

    public static object GetBoxedPort(int port)
    {
        // Reuse pre-boxed instances for the most common ports to avoid allocating.
        var common = port switch
        {
            80 => Port80,
            443 => Port443,
            8080 => Port8080,
            5000 => Port5000,
            5001 => Port5001,
            _ => null,
        };

        if (common is not null)
        {
            return common;
        }

        var last = lastBoxedPort;
        if (last is not null && (int)last == port)
        {
            return last;
        }

        object boxed = port;
        lastBoxedPort = boxed;

        return boxed;
    }

    private static object[] InitializeBoxedStatusCodes()
    {
        var boxedStatusCodes = new object[500];
        for (int i = 0, c = 100; i < boxedStatusCodes.Length; i++, c++)
        {
            boxedStatusCodes[i] = c;
        }

        return boxedStatusCodes;
    }
}
