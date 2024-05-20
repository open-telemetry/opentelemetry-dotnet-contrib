// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// Activity processor that adds <see cref="Baggage"/> fields to every new activity.
/// </summary>
internal sealed class BaggageActivityProcessor : BaseProcessor<Activity>
{
    /// <summary>
    /// A predicate that allows all baggage keys.
    /// </summary>
    public static readonly Predicate<string> AllowAllBaggageKeys = _ => true;

    private readonly Predicate<string> baggageKeyPredicate;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaggageActivityProcessor"/> class.
    /// </summary>
    /// <param name="baggageKeyPredicate">Predicate to determine which baggage keys should be added to the activity.</param>
    public BaggageActivityProcessor(Predicate<string> baggageKeyPredicate)
    {
        this.baggageKeyPredicate = baggageKeyPredicate ?? throw new ArgumentNullException(nameof(baggageKeyPredicate));
    }

    /// <inheritdoc />
    public override void OnStart(Activity activity)
    {
        foreach (var entry in Baggage.Current)
        {
            if (this.baggageKeyPredicate(entry.Key))
            {
                activity.SetTag(entry.Key, entry.Value);
            }
        }
    }
}
