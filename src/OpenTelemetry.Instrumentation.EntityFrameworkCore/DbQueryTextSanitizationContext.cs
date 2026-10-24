// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Contains information about the command passed to a
/// <see cref="EntityFrameworkInstrumentationOptions.QueryTextSanitizer"/> function.
/// </summary>
public readonly struct DbQueryTextSanitizationContext : IEquatable<DbQueryTextSanitizationContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DbQueryTextSanitizationContext"/> struct.
    /// </summary>
    /// <param name="providerName">The database provider name.</param>
    /// <param name="queryText">The unsanitized command text.</param>
    /// <param name="command">The database command.</param>
    internal DbQueryTextSanitizationContext(string? providerName, string? queryText, IDbCommand? command)
    {
        this.ProviderName = providerName;
        this.QueryText = queryText;
        this.Command = command;
    }

    /// <summary>
    /// Gets the name of the Entity Framework Core database provider executing the query.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="null"/> if the provider name could not be determined.
    /// </remarks>
    public string? ProviderName { get; }

    /// <summary>
    /// Gets the unsanitized text of the command being executed.
    /// </summary>
    public string? QueryText { get; }

    /// <summary>
    /// Gets the command being executed, from which additional information can be extracted.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="null"/> if the command is not executed through an
    /// <see cref="IDbCommand"/>.
    /// </remarks>
    public IDbCommand? Command { get; }

    /// <summary>
    /// Determines whether two <see cref="DbQueryTextSanitizationContext"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> when the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(DbQueryTextSanitizationContext left, DbQueryTextSanitizationContext right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="DbQueryTextSanitizationContext"/> instances are different.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> when the instances are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(DbQueryTextSanitizationContext left, DbQueryTextSanitizationContext right) => !left.Equals(right);

    /// <summary>
    /// Determines whether the specified instance is equal to the current instance.
    /// </summary>
    /// <param name="other">The instance to compare with the current instance.</param>
    /// <returns><see langword="true"/> when the instances are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(DbQueryTextSanitizationContext other)
        => string.Equals(this.ProviderName, other.ProviderName, StringComparison.Ordinal)
        && string.Equals(this.QueryText, other.QueryText, StringComparison.Ordinal)
        && ReferenceEquals(this.Command, other.Command);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DbQueryTextSanitizationContext other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Match reference equality even when a provider overrides the command's GetHashCode.
        var commandHashCode = this.Command is null ? 0 : RuntimeHelpers.GetHashCode(this.Command);
#if NET
        return HashCode.Combine(this.ProviderName, this.QueryText, commandHashCode);
#else
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (this.ProviderName is null ? 0 : StringComparer.Ordinal.GetHashCode(this.ProviderName));
            hash = (hash * 31) + (this.QueryText is null ? 0 : StringComparer.Ordinal.GetHashCode(this.QueryText));
            hash = (hash * 31) + commandHashCode;
            return hash;
        }
#endif
    }
}
