// <copyright file="EncodingUtils.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace OpenTelemetry.ResourceDetectors.Container.Utils;

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
