// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// Activity processor that adds the <see cref="Baggage"/> fields which are
/// current when an activity starts to the activity as tags when it ends.
/// <para/>
/// Baggage can be set by callers, so it is added behind the tags set by the
/// application and by instrumentation, and never replaces a tag with the same
/// key. This processor must be added before any exporting processor so that the
/// baggage tags are present when the activity is exported.
/// </summary>
public sealed class BaggageActivityProcessor : BaseProcessor<Activity>
{
    private const string BaggagePropertyName = "OpenTelemetry.Extensions.BaggageActivityProcessor.Baggage";

    private readonly Predicate<string> baggageKeyPredicate;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaggageActivityProcessor"/> class.
    /// </summary>
    /// <param name="baggageKeyPredicate">Predicate to determine which baggage keys should be added to the activity.</param>
    internal BaggageActivityProcessor(Predicate<string> baggageKeyPredicate)
    {
        this.baggageKeyPredicate = baggageKeyPredicate ?? throw new ArgumentNullException(nameof(baggageKeyPredicate));
    }

    /// <summary>
    /// Gets a baggage key predicate that returns <c>true</c> for all baggage keys.
    /// </summary>
    public static Predicate<string> AllowAllBaggageKeys => (_) => true;

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        // Baggage can be set by callers, so it is only captured here and is added
        // to the activity when it ends, behind the tags set by the application and
        // by instrumentation. This stops it from displacing those tags when an
        // exporter applies the span attribute count limit.
        var baggage = Baggage.Current;

        if (data != null && baggage.Count > 0)
        {
            data.SetCustomProperty(BaggagePropertyName, baggage);
        }
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data?.GetCustomProperty(BaggagePropertyName) is Baggage baggage)
        {
            data.SetCustomProperty(BaggagePropertyName, null);

            foreach (var entry in baggage)
            {
                // Avoid overwriting existing tags on the activity with baggage values
                if (this.baggageKeyPredicate(entry.Key) && data.GetTagItem(entry.Key) == null)
                {
                    data.SetTag(entry.Key, entry.Value);
                }
            }
        }
    }
}
