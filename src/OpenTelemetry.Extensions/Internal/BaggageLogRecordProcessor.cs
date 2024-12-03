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

    public static Predicate<string> AllowAllBaggageKeys => (_) => true;

    public override void OnEnd(LogRecord data)
    {
        if (data != null)
        {
            var attributes = new List<KeyValuePair<string, object?>>();

            foreach (var entry in Baggage.Current)
            {
                if (this.baggageKeyPredicate(entry.Key))
                {
                    attributes.Add(new(entry.Key, entry.Value));
                }
            }

            if (data.Attributes != null)
            {
                attributes.AddRange(data.Attributes);
            }

            data.Attributes = attributes;
        }

        base.OnEnd(data!);
    }
}
