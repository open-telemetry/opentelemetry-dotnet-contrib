// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal static class TelemetryHelper
{
    public static readonly object[] BoxedStatusCodes = InitializeBoxedStatusCodes();
    internal static readonly RequestDataHelper RequestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: false);

    private static readonly string[] StatusCodeStrings = InitializeStatusCodeStrings();

    public static object GetBoxedStatusCode(int statusCode) =>
        statusCode is >= 100 and < 600 ? BoxedStatusCodes[statusCode - 100] : statusCode;

    public static string GetStatusCodeString(int statusCode) =>
        statusCode is >= 100 and < 600 ? StatusCodeStrings[statusCode - 100] : statusCode.ToString(CultureInfo.InvariantCulture);

    private static object[] InitializeBoxedStatusCodes()
    {
        var boxedStatusCodes = new object[500];
        for (int i = 0, c = 100; i < boxedStatusCodes.Length; i++, c++)
        {
            boxedStatusCodes[i] = c;
        }

        return boxedStatusCodes;
    }

    private static string[] InitializeStatusCodeStrings()
    {
        var statusCodeStrings = new string[500];
        for (int i = 0, c = 100; i < statusCodeStrings.Length; i++, c++)
        {
            statusCodeStrings[i] = c.ToString(CultureInfo.InvariantCulture);
        }

        return statusCodeStrings;
    }
}
