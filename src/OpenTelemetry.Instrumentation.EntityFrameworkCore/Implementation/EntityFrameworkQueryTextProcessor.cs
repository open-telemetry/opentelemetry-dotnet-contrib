// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

/// <summary>
/// Applies Entity Framework Core query text sanitization and database attributes.
/// </summary>
internal static class EntityFrameworkQueryTextProcessor
{
    public static void Apply(Activity activity, string? commandText, string? providerName, bool emitOldAttributes, bool emitNewAttributes)
    {
        // Only SQL-like providers support sanitization because arbitrary query dialects cannot be sanitized reliably.
        var sanitizeQuery = EntityFrameworkProviderInfo.IsSqlLikeProvider(providerName);
        var useBackslashEscapes = EntityFrameworkProviderInfo.IsBackslashEscapeProvider(providerName);

        DatabaseSemanticConventionHelper.ApplyConventionsForQueryText(
            activity,
            commandText,
            emitOldAttributes,
            emitNewAttributes,
            sanitizeQuery,
            useBackslashEscapes);
    }
}
