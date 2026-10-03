// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Diagnostics;

/// <summary>
/// The <see cref="EventSource"/> for this component's internal diagnostics.
/// </summary>
[EventSource(Name = "OpenTelemetry-DynamicControl")]
internal sealed class DynamicControlEventSource : EventSource
{
    public static readonly DynamicControlEventSource Log = new();

    private const int EventIdPolicyChangeSubscriberFailure = 1;
    private const int EventIdPolicyProviderFetchFailed = 2;
    private const int EventIdPolicyPayloadMalformed = 3;
    private const int EventIdPolicyEntryRejected = 4;
    private const int EventIdPolicyPayloadKeysIgnored = 5;
    private const int EventIdPolicyProviderSubmissionRejected = 6;
    private const int EventIdPolicyProviderSubmissionApplied = 7;
    private const int EventIdPolicyProviderSubmissionSuppressed = 8;
    private const int EventIdPolicyRefreshCompleted = 9;

    /// <summary>
    /// Records that a policy change subscriber's callback threw an exception. The
    /// exception is isolated to the subscription that raised it; it does not affect the
    /// store, other subscribers, or the commit that triggered the notification.
    /// </summary>
    /// <param name="ex">The exception thrown by the subscriber callback.</param>
    [NonEvent]
    public void PolicyChangeSubscriberException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.PolicyChangeSubscriberException(ex.ToInvariantString());
        }
    }

    [Event(EventIdPolicyChangeSubscriberFailure, Message = "Policy change subscriber callback failed: {0}", Level = EventLevel.Warning)]
    public void PolicyChangeSubscriberException(string exception) =>
        this.WriteEvent(EventIdPolicyChangeSubscriberFailure, exception);

    /// <summary>
    /// Records that a policy provider's fetch threw an unexpected exception.
    /// </summary>
    /// <param name="registrationId">The identity of the provider that failed.</param>
    /// <param name="exception">The exception thrown by the provider.</param>
    [NonEvent]
    public void PolicyProviderFetchException(string registrationId, Exception exception)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.PolicyProviderFetchException(registrationId, exception.ToInvariantString());
        }
    }

    [Event(EventIdPolicyProviderFetchFailed, Message = "Policy provider fetch failed for '{0}': {1}", Level = EventLevel.Error)]
    public void PolicyProviderFetchException(string registrationId, string exception) =>
        this.WriteEvent(EventIdPolicyProviderFetchFailed, registrationId, exception);

    /// <summary>
    /// Records that a policy payload could not be decoded at all. No submission is made;
    /// the provider's previously accepted snapshot remains in place.
    /// </summary>
    /// <param name="registrationId">The identity of the provider that supplied the payload.</param>
    /// <param name="error">The parser's description of why the payload could not be decoded.</param>
    [Event(EventIdPolicyPayloadMalformed, Message = "Policy payload from '{0}' could not be decoded: {1}", Level = EventLevel.Warning)]
    public void PolicyPayloadMalformed(string registrationId, string error) =>
        this.WriteEvent(EventIdPolicyPayloadMalformed, registrationId, error);

    /// <summary>
    /// Records that a single decoded entry could not be turned into a valid policy and
    /// was excluded from the committed set.
    /// </summary>
    /// <param name="registrationId">The identity of the provider that supplied the payload.</param>
    /// <param name="location">The pre-formatted location of the rejected entry within the payload.</param>
    /// <param name="reason">The rejection reason category.</param>
    /// <param name="message">A description of why the entry was rejected.</param>
    [Event(EventIdPolicyEntryRejected, Message = "Policy entry from '{0}' at {1} rejected ({2}): {3}", Level = EventLevel.Warning)]
    public void PolicyEntryRejected(string registrationId, string location, string reason, string message) =>
        this.WriteEvent(EventIdPolicyEntryRejected, registrationId, location, reason, message);

    /// <summary>
    /// Records that a decoded payload contained keys this package does not recognize.
    /// The payload's recognized entries are still committed.
    /// </summary>
    /// <param name="registrationId">The identity of the provider that supplied the payload.</param>
    /// <param name="ignoredKeyCount">The number of distinct unrecognized keys.</param>
    [Event(EventIdPolicyPayloadKeysIgnored, Message = "Policy payload from '{0}' contained {1} unrecognized key(s).", Level = EventLevel.Verbose)]
    public void PolicyPayloadKeysIgnored(string registrationId, int ignoredKeyCount) =>
        this.WriteEvent(EventIdPolicyPayloadKeysIgnored, registrationId, ignoredKeyCount);

    /// <summary>
    /// Records that a provider submission was rejected. This indicates either a coordinator
    /// bug (snapshot creation failed) or an unexpected store rejection.
    /// </summary>
    /// <param name="registrationId">The identity of the provider whose submission was rejected.</param>
    /// <param name="error">A description of why the submission was rejected.</param>
    [Event(EventIdPolicyProviderSubmissionRejected, Message = "Policy provider submission rejected for '{0}': {1}", Level = EventLevel.Error)]
    public void PolicyProviderSubmissionRejected(string registrationId, string error) =>
        this.WriteEvent(EventIdPolicyProviderSubmissionRejected, registrationId, error);

    /// <summary>
    /// Records that a provider submission was accepted and the store revision advanced.
    /// </summary>
    /// <param name="registrationId">The identity of the provider whose submission was applied.</param>
    /// <param name="sequence">The sequence number of the applied submission.</param>
    /// <param name="revision">The new store revision after applying the submission.</param>
    /// <param name="policyCount">The number of policies in the applied snapshot.</param>
    [Event(EventIdPolicyProviderSubmissionApplied, Message = "Policy provider submission applied for '{0}': sequence={1}, revision={2}, policies={3}", Level = EventLevel.Verbose)]
    public void PolicyProviderSubmissionApplied(string registrationId, long sequence, long revision, int policyCount) =>
        this.WriteEvent(EventIdPolicyProviderSubmissionApplied, registrationId, sequence, revision, policyCount);

    /// <summary>
    /// Records that a provider submission was suppressed because its version was unchanged.
    /// </summary>
    /// <param name="registrationId">The identity of the provider whose submission was suppressed.</param>
    /// <param name="sequence">The sequence number of the suppressed submission.</param>
    [Event(EventIdPolicyProviderSubmissionSuppressed, Message = "Policy provider submission suppressed for '{0}': version unchanged, sequence={1}", Level = EventLevel.Verbose)]
    public void PolicyProviderSubmissionSuppressed(string registrationId, long sequence) =>
        this.WriteEvent(EventIdPolicyProviderSubmissionSuppressed, registrationId, sequence);

    /// <summary>
    /// Records that a full refresh cycle completed normally.
    /// </summary>
    /// <param name="providerCount">The number of providers iterated during the refresh.</param>
    /// <param name="elapsed">The elapsed time for the full refresh.</param>
    [NonEvent]
    public void PolicyRefreshCompleted(int providerCount, TimeSpan elapsed)
    {
        if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All))
        {
            this.PolicyRefreshCompleted(providerCount, elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + "ms");
        }
    }

    [Event(EventIdPolicyRefreshCompleted, Message = "Policy refresh completed: providers={0}, elapsed={1}", Level = EventLevel.Verbose)]
    public void PolicyRefreshCompleted(int providerCount, string elapsed) =>
        this.WriteEvent(EventIdPolicyRefreshCompleted, providerCount, elapsed);
}
