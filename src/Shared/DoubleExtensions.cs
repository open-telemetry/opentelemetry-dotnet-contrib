// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET && !NETSTANDARD2_1_OR_GREATER

using System.Runtime.CompilerServices;

namespace System;

internal static class DoubleExtensions
{
    extension(double)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsNegative(double value) =>
            BitConverter.DoubleToInt64Bits(value) < 0;
    }
}

#endif
