// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

internal static class TelemetryHelper
{
    public static readonly (object, string)[] BoxedStatusCodes = InitializeBoxedStatusCodes();

    private static readonly object Port80 = 80;
    private static readonly object Port443 = 443;
    private static readonly object Port8080 = 8080;

    private static object? lastBoxedPort;

    public static object GetBoxedStatusCode(HttpStatusCode statusCode)
    {
        var intStatusCode = (int)statusCode;
        return intStatusCode is >= 100 and < 600 ? BoxedStatusCodes[intStatusCode - 100].Item1 : statusCode;
    }

    public static string GetStatusCodeString(HttpStatusCode statusCode)
    {
        var intStatusCode = (int)statusCode;
        return intStatusCode is >= 100 and < 600 ? BoxedStatusCodes[intStatusCode - 100].Item2 : statusCode.ToString();
    }

    public static object GetBoxedPort(int port)
    {
        var common = port switch
        {
            80 => Port80,
            443 => Port443,
            8080 => Port8080,
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

    private static (object, string)[] InitializeBoxedStatusCodes()
    {
        var boxedStatusCodes = new (object, string)[500];
        for (int i = 0, c = 100; i < boxedStatusCodes.Length; i++, c++)
        {
            boxedStatusCodes[i] = (c, c.ToString(CultureInfo.InvariantCulture));
        }

        return boxedStatusCodes;
    }
}
