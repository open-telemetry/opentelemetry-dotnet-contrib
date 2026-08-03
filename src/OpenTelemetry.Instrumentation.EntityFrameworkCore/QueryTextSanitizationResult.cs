// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Describes the result of a
/// <see cref="EntityFrameworkInstrumentationOptions.QueryTextSanitizer"/> function.
/// </summary>
/// <remarks>
/// Use <see cref="NotSanitized"/> to emit the command text unchanged, or
/// <see cref="Sanitized(string, string)"/> to emit something else in its place.
/// </remarks>
#pragma warning disable CA1815 // Override equals and operator equals on value types
public readonly struct QueryTextSanitizationResult
#pragma warning restore CA1815 // Override equals and operator equals on value types
{
    private QueryTextSanitizationResult(string? queryText, string? querySummary)
    {
        this.IsSanitized = true;
        this.QueryText = queryText;
        this.QuerySummary = querySummary;
    }

    /// <summary>
    /// Gets a result that emits the original command text unchanged.
    /// </summary>
    public static QueryTextSanitizationResult NotSanitized => default;

    /// <summary>
    /// Gets a value indicating whether the query text was sanitized.
    /// </summary>
    public bool IsSanitized { get; }

    /// <summary>
    /// Gets the query text to emit, or <see langword="null"/> if the query text should
    /// not be emitted.
    /// </summary>
    public string? QueryText { get; }

    /// <summary>
    /// Gets the query summary to emit, or <see langword="null"/> if no summary is available.
    /// </summary>
    /// <remarks>
    /// The summary is only emitted when the new database attributes are enabled.
    /// </remarks>
    public string? QuerySummary { get; }

    /// <summary>
    /// Creates a result that emits the supplied query text.
    /// </summary>
    /// <param name="queryText">
    /// The query text to emit, or <see langword="null"/> to not emit the
    /// <c>db.query.text</c> and <c>db.statement</c> attributes.
    /// </param>
    /// <param name="querySummary">
    /// The query summary to emit as <c>db.query.summary</c>, which is also used as the
    /// <see cref="Activity.DisplayName"/>. When <see langword="null"/> or empty, both
    /// are left unchanged.
    /// </param>
    /// <returns>The <see cref="QueryTextSanitizationResult"/>.</returns>
    public static QueryTextSanitizationResult Sanitized(string? queryText, string? querySummary = null)
        => new(queryText, querySummary);
}
