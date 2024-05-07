// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal static class TelemetryHelper
{
    public static readonly object[] BoxedStatusCodes = InitializeBoxedStatusCodes();

    public static object GetBoxedStatusCode(int statusCode)
    {
        if (statusCode >= 100 && statusCode < 600)
        {
            return BoxedStatusCodes[statusCode - 100];
        }

        return statusCode;
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
