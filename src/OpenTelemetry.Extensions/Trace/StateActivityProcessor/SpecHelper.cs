// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// Helper class for specification-related operations.
/// </summary>
public abstract class SpecHelper
{
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false, // For pretty printing
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // To allow special characters
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes the <see cref="TracesData"/> to a JSON string per specification.
    /// </summary>
    /// <param name="tracesData">TracesData per spec.</param>
    /// <returns>TracesData in JSON format.</returns>
    public static string Json(TracesData tracesData) =>
        JsonSerializer.Serialize(tracesData, CachedJsonSerializerOptions);

    /// <summary>
    /// Converts a <see cref="DateTime"/> to Unix time in nanoseconds.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>Unix time representation in nanoseconds.</returns>
    public static ulong ToUnixTimeNanoseconds(DateTime dateTime)
    {
        dateTime = dateTime.ToUniversalTime();

        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Ticks since epoch (1 tick = 100ns)
        long ticksSinceEpoch = dateTime.Ticks - epoch.Ticks;

        // Convert to nanoseconds (multiply by 100)
        return (ulong)(ticksSinceEpoch * 100);
    }
}
