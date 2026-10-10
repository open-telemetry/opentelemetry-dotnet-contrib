// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

namespace OpenTelemetry.Extensions.Internal;

internal sealed class BaggageLogRecordProcessor : BaseProcessor<LogRecord>
{
    private readonly Predicate<string> baggageKeyPredicate;

    public BaggageLogRecordProcessor(Predicate<string> baggageKeyPredicate)
    {
        this.baggageKeyPredicate = baggageKeyPredicate ?? throw new ArgumentNullException(nameof(baggageKeyPredicate));
    }

    public static Predicate<string> AllowAllBaggageKeys => static (_) => true;

    public override void OnEnd(LogRecord data)
    {
        var baggage = Baggage.Current;

        if (data != null && baggage.Count > 0)
        {
            var recordAttributes = data.Attributes;
            var capacity = (recordAttributes?.Count ?? 0) + baggage.Count;
            var attributes = new List<KeyValuePair<string, object?>>(capacity);

            if (recordAttributes != null)
            {
                attributes.AddRange(recordAttributes);
            }

            foreach (var entry in baggage)
            {
                // Avoid overwriting existing attributes in the log record with baggage values
                if (this.baggageKeyPredicate(entry.Key) && !ContainsKey(recordAttributes, entry.Key))
                {
                    attributes.Add(new(entry.Key, entry.Value));
                }
            }

            data.Attributes = attributes;
        }

        base.OnEnd(data!);
    }

    private static bool ContainsKey(IReadOnlyList<KeyValuePair<string, object?>>? attributes, string key)
    {
        if (attributes != null)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                if (string.Equals(attributes[i].Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
