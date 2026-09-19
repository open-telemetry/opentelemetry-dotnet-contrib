// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Represents a validated policy setting the severity threshold for the OpenTelemetry
/// SDK's own diagnostic logs.
/// </summary>
internal sealed class LogLevelPolicy : TelemetryPolicy
{
    /// <summary>
    /// The <see cref="TelemetryPolicy.PolicyType"/> value for this policy type.
    /// </summary>
    internal static readonly PolicyType PolicyTypeValue = new("log-level");

    private LogLevelPolicy(PolicyId id, string name, DiagnosticLogLevel minimumLevel)
        : base(id, name)
    {
        this.MinimumLevel = minimumLevel;
    }

    /// <inheritdoc/>
    public override PolicyType PolicyType => PolicyTypeValue;

    /// <summary>
    /// Gets the severity at or above which diagnostic logs are emitted.
    /// </summary>
    public DiagnosticLogLevel MinimumLevel { get; }

    /// <summary>
    /// Attempts to create a validated <see cref="LogLevelPolicy"/>.
    /// </summary>
    /// <param name="id">The provider-assigned policy identifier. Must not be <see cref="PolicyId.Empty"/>.</param>
    /// <param name="name">The human-readable policy name. Must not be null or whitespace.</param>
    /// <param name="minimumLevel">
    /// The severity threshold. Must be a usable <see cref="DiagnosticLogLevel"/> member.
    /// </param>
    /// <param name="policy">
    /// When this method returns <see langword="true"/>, the newly created policy; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="error">
    /// When this method returns <see langword="false"/>, a message describing why validation failed;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if all arguments are valid and <paramref name="policy"/> was created;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryCreate(
        PolicyId id,
        string name,
        DiagnosticLogLevel minimumLevel,
        [NotNullWhen(true)] out LogLevelPolicy? policy,
        [NotNullWhen(false)] out string? error)
    {
        if (id.IsEmpty)
        {
            policy = null;
            error = "The policy ID is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            policy = null;
            error = "The policy name is required.";
            return false;
        }

        if (!IsSupported(minimumLevel))
        {
            policy = null;
            error = "The log level must be a supported severity.";
            return false;
        }

        policy = new LogLevelPolicy(id, name, minimumLevel);
        error = null;
        return true;
    }

    private static bool IsSupported(DiagnosticLogLevel level)
        => level is DiagnosticLogLevel.Trace
            or DiagnosticLogLevel.Debug
            or DiagnosticLogLevel.Information
            or DiagnosticLogLevel.Warning
            or DiagnosticLogLevel.Error
            or DiagnosticLogLevel.None;
}
