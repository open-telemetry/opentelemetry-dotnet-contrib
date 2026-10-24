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
        var result = SanitizeQueryText(new DbQueryTextSanitizationContext(providerName, commandText, null));

        if (result.SuppressQueryText)
        {
            return;
        }

        if (result.IsSanitized)
        {
            DatabaseSemanticConventionHelper.ApplyConventionsForSanitizedQueryText(
                activity,
                result.QueryText ?? string.Empty,
                result.QuerySummary,
                emitOldAttributes,
                emitNewAttributes);
            return;
        }

        DatabaseSemanticConventionHelper.ApplyConventionsForQueryText(
            activity,
            commandText,
            emitOldAttributes,
            emitNewAttributes,
            sanitizeQuery: false);
    }

    internal static DbQueryTextSanitizationResult SanitizeQueryText(DbQueryTextSanitizationContext context)
    {
        if (!EntityFrameworkProviderInfo.IsSqlLikeProvider(context.ProviderName))
        {
            return DbQueryTextSanitizationResult.NotSanitized;
        }

        var useBackslashEscapes = EntityFrameworkProviderInfo.IsBackslashEscapeProvider(context.ProviderName);
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(context.QueryText, useBackslashEscapes);

        return DbQueryTextSanitizationResult.Sanitized(sqlStatementInfo.SanitizedSql, sqlStatementInfo.DbQuerySummary);
    }
}
