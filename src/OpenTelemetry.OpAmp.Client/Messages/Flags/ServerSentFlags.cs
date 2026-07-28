// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents the <c>ServerToAgentFlags</c> enumeration.
/// </summary>
[Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32. ServerToAgent is using ulong backing type.
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix. The name is more closely related to ServerToAgentFlags.
public enum ServerSentFlags : ulong
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
#pragma warning restore CA1028 // Enum Storage should be Int32
{
    /// <summary>
    /// Nothing specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// This flag can be used by the Server if the Agent did not include the
    /// particular bit of information in the last status report (which is an allowed
    /// optimization) but the Server detects that it does not have it (e.g. was
    /// restarted and lost state).
    /// The Server asks the Agent to report its full status.
    /// </summary>
    ReportFullState = 1,

    /// <summary>
    /// This flag can be used by the server if the Agent did
    /// not include the full AvailableComponents message, but only the hash.
    /// If this flag is specified, the agent will populate reported available components
    /// with a full description of the agent's components.
    /// </summary>
    ReportAvailableComponents = 2,
}
