// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace System;

#if !NET

internal static class EnumExtensions
{
    extension(Enum)
    {
        internal static TEnum[] GetValues<TEnum>()
            where TEnum : struct
            => (TEnum[])Enum.GetValues(typeof(TEnum));
    }
}

#endif
