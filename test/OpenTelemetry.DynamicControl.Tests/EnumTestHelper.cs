// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Tests;

internal static class EnumTestHelper
{
    // The generic Enum.GetValues<TEnum>() overload that CA2263 prefers is not available
    // on .NET Framework, so the choice is made here rather than at every call site.
    public static TEnum[] Values<TEnum>()
        where TEnum : struct, Enum
#if NET
        => Enum.GetValues<TEnum>();
#else
        => (TEnum[])Enum.GetValues(typeof(TEnum));
#endif
}
