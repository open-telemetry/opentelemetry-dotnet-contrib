// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents ServerToAgentFlags enum.
/// </summary>
[Flags]
public enum ServerCommands
{
    /// <summary>
    /// Nothing specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// ReportFullState flag can be used by the Server if the Agent did not include the
    /// particular bit of information in the last status report (which is an allowed
    /// optimization) but the Server detects that it does not have it (e.g. was
    /// restarted and lost state). The detection happens using
    /// AgentToServer.sequence_num values.
    /// The Server asks the Agent to report full status.
    /// </summary>
    ReportFullState = 1,

    /// <summary>
    /// ReportAvailableComponents flag can be used by the server if the Agent did
    /// not include the full AvailableComponents message, but only the hash.
    /// If this flag is specified, the agent will populate available_components.components
    /// with a full description of the agent's components.
    /// Status: [Development].
    /// </summary>
    ReportAvailableComponents = 2,
}
