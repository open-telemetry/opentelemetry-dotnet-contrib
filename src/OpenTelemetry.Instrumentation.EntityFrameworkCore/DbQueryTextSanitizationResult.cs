// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Describes the result of a
/// <see cref="EntityFrameworkInstrumentationOptions.QueryTextSanitizer"/> function.
/// </summary>
/// <remarks>
/// Use <see cref="NotSanitized"/> to emit the original command text unchanged,
/// <see cref="Suppress"/> to avoid emitting query text, or
/// <see cref="Sanitized(string, string?)"/> to emit sanitized query text.
/// </remarks>
public readonly struct DbQueryTextSanitizationResult : IEquatable<DbQueryTextSanitizationResult>
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
    public static DbQueryTextSanitizationResult NotSanitized { get; } =
        new(isSanitized: false, suppressQueryText: false, queryText: null, querySummary: null);

    /// <summary>
    /// Gets a result that does not emit query text.
    /// </summary>
    public static DbQueryTextSanitizationResult Suppress { get; } =
        new(isSanitized: false, suppressQueryText: true, queryText: null, querySummary: null);

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
    /// Determines whether two <see cref="DbQueryTextSanitizationResult"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> when the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(DbQueryTextSanitizationResult left, DbQueryTextSanitizationResult right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="DbQueryTextSanitizationResult"/> instances are different.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> when the instances are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(DbQueryTextSanitizationResult left, DbQueryTextSanitizationResult right) => !left.Equals(right);

    /// <summary>
    /// Creates a result that emits sanitized query text.
    /// </summary>
    /// <param name="queryText">The sanitized query text to emit.</param>
    /// <param name="querySummary">The query summary to emit as <c>db.query.summary</c>.</param>
    /// <returns>The <see cref="DbQueryTextSanitizationResult"/>.</returns>
    public static DbQueryTextSanitizationResult Sanitized(string queryText, string? querySummary = null)
        => new(isSanitized: true, suppressQueryText: false, queryText, querySummary);

    /// <inheritdoc/>
    public bool Equals(DbQueryTextSanitizationResult other)
        => this.IsSanitized == other.IsSanitized
        && this.SuppressQueryText == other.SuppressQueryText
        && string.Equals(this.QueryText, other.QueryText, StringComparison.Ordinal)
        && string.Equals(this.QuerySummary, other.QuerySummary, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DbQueryTextSanitizationResult other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET
        return HashCode.Combine(this.IsSanitized, this.SuppressQueryText, this.QueryText, this.QuerySummary);
#else
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + this.IsSanitized.GetHashCode();
            hash = (hash * 31) + this.SuppressQueryText.GetHashCode();
            hash = (hash * 31) + (this.QueryText is null ? 0 : StringComparer.Ordinal.GetHashCode(this.QueryText));
            hash = (hash * 31) + (this.QuerySummary is null ? 0 : StringComparer.Ordinal.GetHashCode(this.QuerySummary));
            return hash;
        }
#endif
    }
}
