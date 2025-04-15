// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Context;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// A processor that enriches log records with AFDCorrelationId if it's present in the RuntimeContext.
/// </summary>
internal class AFDCorrelationIdLogProcessor : BaseProcessor<LogRecord>
{
    private const string AFDCorrelationId = "AFDCorrelationId";

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        if (data == null)
        {
            return;
        }

        string? afdCorrelationIdValue = GetRuntimeContextValue();
        if (string.IsNullOrEmpty(afdCorrelationIdValue))
        {
            base.OnEnd(data);
            return;
        }

        int capacity = data.Attributes?.Count + 1 ?? 1;
        var attributes = new List<KeyValuePair<string, object?>>(capacity);

        if (data.Attributes != null)
        {
            attributes.AddRange(data.Attributes);
        }

        attributes.Add(new KeyValuePair<string, object?>(AFDCorrelationId, afdCorrelationIdValue));
        data.Attributes = attributes;
        base.OnEnd(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? GetRuntimeContextValue() => RuntimeContext.GetValue<string>(AFDCorrelationId);
}
