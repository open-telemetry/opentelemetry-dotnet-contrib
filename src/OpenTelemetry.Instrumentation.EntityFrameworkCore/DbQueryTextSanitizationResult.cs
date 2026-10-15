// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Describes the result of sanitizing database command text.
/// </summary>
/// <remarks>
/// Use <see cref="NotSanitized"/> to emit the original command text unchanged,
/// <see cref="Suppress"/> to avoid emitting query text, or
/// <see cref="Sanitized(string, string?)"/> to emit sanitized query text.
/// </remarks>
#pragma warning disable CA1815 // Override equals and operator equals on value types
internal readonly struct DbQueryTextSanitizationResult
#pragma warning restore CA1815 // Override equals and operator equals on value types
{
    private DbQueryTextSanitizationResult(bool isSanitized, bool suppressQueryText, string? queryText, string? querySummary)
    {
        this.IsSanitized = isSanitized;
        this.SuppressQueryText = suppressQueryText;
        this.QueryText = queryText;
        this.QuerySummary = querySummary;
    }

    /// <summary>
    /// Gets a result that emits the original command text unchanged.
    /// </summary>
    public static DbQueryTextSanitizationResult NotSanitized => default;

    /// <summary>
    /// Gets a result that does not emit query text.
    /// </summary>
    public static DbQueryTextSanitizationResult Suppress => new(isSanitized: false, suppressQueryText: true, queryText: null, querySummary: null);

    /// <summary>
    /// Gets a value indicating whether the query text was sanitized.
    /// </summary>
    public bool IsSanitized { get; }

    /// <summary>
    /// Gets a value indicating whether query text should not be emitted.
    /// </summary>
    public bool SuppressQueryText { get; }

    /// <summary>
    /// Gets the query text to emit.
    /// </summary>
    public string? QueryText { get; }

    /// <summary>
    /// Gets the query summary to emit.
    /// </summary>
    /// <remarks>
    /// The summary is only emitted when the new database attributes are enabled.
    /// </remarks>
    public string? QuerySummary { get; }

    /// <summary>
    /// Creates a result that emits sanitized query text.
    /// </summary>
    /// <param name="queryText">The sanitized query text to emit.</param>
    /// <param name="querySummary">The query summary to emit as <c>db.query.summary</c>.</param>
    /// <returns>The <see cref="DbQueryTextSanitizationResult"/>.</returns>
    public static DbQueryTextSanitizationResult Sanitized(string queryText, string? querySummary = null)
        => new(isSanitized: true, suppressQueryText: false, queryText, querySummary);
}
