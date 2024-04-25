// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

internal static class TelemetryHelper
{
    public static readonly (object, string)[] BoxedStatusCodes = InitializeBoxedStatusCodes();

    public static object GetBoxedStatusCode(HttpStatusCode statusCode)
    {
        int intStatusCode = (int)statusCode;
        if (intStatusCode >= 100 && intStatusCode < 600)
        {
            return BoxedStatusCodes[intStatusCode - 100].Item1;
        }

        return statusCode;
    }

    public static string GetStatusCodeString(HttpStatusCode statusCode)
    {
        int intStatusCode = (int)statusCode;
        if (intStatusCode >= 100 && intStatusCode < 600)
        {
            return BoxedStatusCodes[intStatusCode - 100].Item2;
        }

        return statusCode.ToString();
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
