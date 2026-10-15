// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Contains information about a database command to sanitize.
/// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
internal readonly struct DbQueryTextSanitizationContext
#pragma warning restore CA1815 // Override equals and operator equals on value types
{
    internal DbQueryTextSanitizationContext(
        string? providerName,
        string? queryText,
        IDbCommand? command)
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
    /// <remarks>
    /// <para>
    /// <b>WARNING: The raw command text may contain sensitive data, such as literal values
    /// embedded in the query.</b>
    /// </para>
    /// </remarks>
    public string? QueryText { get; }

    /// <summary>
    /// Gets the command being executed, from which additional information can be extracted.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="null"/> if the command is not executed through an
    /// <see cref="IDbCommand"/>.
    /// </remarks>
    public IDbCommand? Command { get; }
}
