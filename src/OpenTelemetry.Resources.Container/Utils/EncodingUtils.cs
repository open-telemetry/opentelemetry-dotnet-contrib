// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;

namespace OpenTelemetry.Resources.Container.Utils;

internal class EncodingUtils
{
    /// <summary>
    /// Checks if the string is valid hex.
    /// </summary>
    /// <param name="hexString">string.</param>
    /// <returns>true if valid else false.</returns>
    public static bool IsValidHexString(IEnumerable<char> hexString)
    {
        return hexString.All(currentCharacter =>
            (currentCharacter >= '0' && currentCharacter <= '9') ||
            (currentCharacter >= 'a' && currentCharacter <= 'f') ||
            (currentCharacter >= 'A' && currentCharacter <= 'F'));
    }
}
