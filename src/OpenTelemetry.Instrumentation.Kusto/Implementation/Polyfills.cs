// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class Polyfills
{
#if NETFRAMEWORK || NETSTANDARD
    public static bool StartsWith(this string target, char value)
    {
        if (target.Length == 0)
        {
            return false;
        }

        return target[0] == value;
    }
#endif
}
