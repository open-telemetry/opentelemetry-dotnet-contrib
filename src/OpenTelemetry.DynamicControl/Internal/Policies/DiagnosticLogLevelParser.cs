// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Converts the textual severity tokens of the <c>OTEL_LOG_LEVEL</c> convention into
/// <see cref="DiagnosticLogLevel"/> members.
/// </summary>
/// <remarks>
/// The accepted tokens are matched without regard to case and permitting surrounding
/// white space.
/// </remarks>
internal static class DiagnosticLogLevelParser
{
    /// <summary>
    /// The accepted tokens, formatted for use in an error message.
    /// </summary>
    public static readonly string AcceptedTokens = FormatAcceptedTokens(CreateAcceptedTokenValues());

    /// <summary>
    /// The accepted tokens, in the order the error message names them.
    /// </summary>
    internal static readonly ImmutableArray<string> AcceptedTokenValues = CreateAcceptedTokenValues();

    /// <summary>
    /// Attempts to convert a severity token into the member it names.
    /// </summary>
    /// <param name="text">
    /// The token to convert. Surrounding white space is permitted. May be
    /// <see langword="null"/>, which names no member.
    /// </param>
    /// <param name="level">
    /// When this method returns <see langword="true"/>, the member the token names;
    /// otherwise <see cref="DiagnosticLogLevel.Unspecified"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the token names a member; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? text, out DiagnosticLogLevel level)
    {
        if (text is null)
        {
            level = DiagnosticLogLevel.Unspecified;
            return false;
        }

        var token = text.AsSpan().Trim();

        level = token.Length switch
        {
            4 when token.Equals("info", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Information,
            4 when token.Equals("warn", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Warning,
            4 when token.Equals("none", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.None,
            5 when token.Equals("trace", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Trace,
            5 when token.Equals("debug", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Debug,
            5 when token.Equals("error", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Error,
            7 when token.Equals("warning", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Warning,
            11 when token.Equals("information", StringComparison.OrdinalIgnoreCase) => DiagnosticLogLevel.Information,
            _ => DiagnosticLogLevel.Unspecified,
        };

        return level != DiagnosticLogLevel.Unspecified;
    }

    private static ImmutableArray<string> CreateAcceptedTokenValues() =>
        ["trace", "debug", "info", "information", "warn", "warning", "error", "none"];

    private static string FormatAcceptedTokens(ImmutableArray<string> tokens)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < tokens.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            if (i == tokens.Length - 1)
            {
                builder.Append("or ");
            }

            builder.Append('\'').Append(tokens[i]).Append('\'');
        }

        return builder.ToString();
    }
}
