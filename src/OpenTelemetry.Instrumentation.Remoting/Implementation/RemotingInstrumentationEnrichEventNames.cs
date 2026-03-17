// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Remoting;

/// <summary>
/// Defines the event names used by the Remoting instrumentation Enrich callback.
/// </summary>
internal static class RemotingInstrumentationEnrichEventNames
{
    /// <summary>
    /// Event name for when a remoting message starts processing.
    /// </summary>
    internal const string OnMessageStart = "OnMessageStart";

    /// <summary>
    /// Event name for when a remoting message finishes processing.
    /// </summary>
    internal const string OnMessageFinish = "OnMessageFinish";
}
