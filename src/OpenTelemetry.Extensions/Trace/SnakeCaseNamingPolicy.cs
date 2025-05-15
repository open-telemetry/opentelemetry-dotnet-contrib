// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Extensions.Trace;

/// <summary>
/// A naming policy that converts property names to snake_case.
/// </summary>
public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    /// <inheritdoc />
    public override string ConvertName(string name) =>
        string.Concat(
            name.Select<char, object>((ch, i) =>
                i > 0 && char.IsUpper(ch)
                    ? "_" + char.ToLower(ch, CultureInfo.CurrentCulture)
                    : char.ToLower(ch, CultureInfo.CurrentCulture)));
}
