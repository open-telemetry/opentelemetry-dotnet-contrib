// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

public class SpecHelper
{
    public static string Json(TracesData tracesData) =>
        JsonSerializer.Serialize(tracesData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false, // For pretty printing
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // To allow special characters
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition
                    .WhenWritingNull
        });

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
