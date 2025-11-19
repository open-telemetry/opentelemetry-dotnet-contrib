// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class StringBuilderExtensions
{
    public static StringBuilder TrimEnd(this StringBuilder sb)
    {
        if (sb.Length == 0)
        {
            return sb;
        }

        int i = sb.Length - 1;

        for (; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(sb[i]))
            {
                break;
            }
        }

        if (i < sb.Length - 1)
        {
            sb.Length = i + 1;
        }

        return sb;
    }
}
