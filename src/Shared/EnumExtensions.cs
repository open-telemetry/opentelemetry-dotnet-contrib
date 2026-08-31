// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET

namespace System;
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
