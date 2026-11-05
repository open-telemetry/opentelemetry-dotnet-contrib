// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// Activity processor that adds the <see cref="Baggage"/> fields which are
/// current when an activity starts to the activity as tags when it ends.
/// <para/>
/// Baggage can be set by callers, so it is added behind the tags set while the
/// activity is running (by the application and by instrumentation), and never
/// replaces a tag with the same key.
/// <para/>
/// Processors run in the order they are added, so the order matters: processors
/// which add tags to an activity when it ends must be added before this
/// processor, and this processor must be added before any exporting processor,
/// i.e. enrichers, then baggage, then exporters. Tags added by a processor which
/// runs after this one are placed behind the baggage tags, and so can be dropped
/// by an exporter which applies a limit to the number of tags.
/// </summary>
public sealed class BaggageActivityProcessor : BaseProcessor<Activity>
{
    private static int instanceCount;

    private readonly string baggagePropertyName;
    private readonly Predicate<string> baggageKeyPredicate;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaggageActivityProcessor"/> class.
    /// </summary>
    /// <param name="baggageKeyPredicate">Predicate to determine which baggage keys should be added to the activity.</param>
    internal BaggageActivityProcessor(Predicate<string> baggageKeyPredicate)
    {
        this.baggageKeyPredicate = baggageKeyPredicate ?? throw new ArgumentNullException(nameof(baggageKeyPredicate));

        // Each instance uses its own property so that multiple instances
        // (e.g. with different predicates) do not consume each other's baggage.
        this.baggagePropertyName = $"OpenTelemetry.Extensions.BaggageActivityProcessor.Baggage.{Interlocked.Increment(ref instanceCount)}";
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
            data.SetCustomProperty(this.baggagePropertyName, baggage);
        }
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data?.GetCustomProperty(this.baggagePropertyName) is Baggage baggage)
        {
            data.SetCustomProperty(this.baggagePropertyName, null);

            foreach (var entry in baggage)
            {
                // Avoid overwriting existing tags on the activity with baggage values
                if (this.baggageKeyPredicate(entry.Key) && !HasTag(data, entry.Key))
                {
                    data.SetTag(entry.Key, entry.Value);
                }
            }
        }
    }

    private static bool HasTag(Activity activity, string key)
    {
        // GetTagItem cannot distinguish a missing tag from one with a null value
        foreach (var tag in activity.EnumerateTagObjects())
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
