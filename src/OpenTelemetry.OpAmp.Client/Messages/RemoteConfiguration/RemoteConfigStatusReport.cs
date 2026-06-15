// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents an OpAMP remote configuration status report.
/// </summary>
public sealed class RemoteConfigStatusReport
{
    private readonly byte[] lastRemoteConfigHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteConfigStatusReport"/> class.
    /// </summary>
    /// <param name="lastRemoteConfigHash">Hash bytes from the last received remote configuration.</param>
    /// <param name="status">The remote configuration status.</param>
    /// <param name="errorMessage">Optional error message when <paramref name="status"/> is <see cref="RemoteConfigStatusCode.Failed"/>.</param>
    public RemoteConfigStatusReport(
        ReadOnlyMemory<byte> lastRemoteConfigHash,
        RemoteConfigStatusCode status,
        string? errorMessage = null)
    {
        if (status is < RemoteConfigStatusCode.Unset or > RemoteConfigStatusCode.Failed)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported remote configuration status.");
        }

        this.lastRemoteConfigHash = lastRemoteConfigHash.ToArray();
        this.Status = status;
        this.ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the hash bytes from the last received remote configuration.
    /// </summary>
    public ReadOnlyMemory<byte> LastRemoteConfigHash => this.lastRemoteConfigHash;

    /// <summary>
    /// Gets the remote configuration status.
    /// </summary>
    public RemoteConfigStatusCode Status { get; }

    /// <summary>
    /// Gets the optional error message.
    /// </summary>
    public string? ErrorMessage { get; }
}
