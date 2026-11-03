// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal static class TelemetryHelper
{
    private static readonly object[] BoxedStatusCodes = InitializeBoxedStatusCodes();

    /// <summary>
    /// Gets a boxed instance of <paramref name="statusCode"/>, avoiding a boxing allocation
    /// for the status codes which are valid for an HTTP response.
    /// </summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    /// <returns>A boxed <see cref="int"/> equal to <paramref name="statusCode"/>.</returns>
    public static object GetBoxedStatusCode(int statusCode)
        => statusCode is >= 100 and < 600 ? BoxedStatusCodes[statusCode - 100] : statusCode;

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
