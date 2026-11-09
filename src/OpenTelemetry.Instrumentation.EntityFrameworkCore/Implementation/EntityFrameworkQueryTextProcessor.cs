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

        ApplyResult(activity, commandText, result, emitOldAttributes, emitNewAttributes);
    }

    internal static void ApplyResult(Activity activity, string? commandText, DbQueryTextSanitizationResult result, bool emitOldAttributes, bool emitNewAttributes)
    {
        if (result.SuppressQueryText)
        {
            return;
        }

        if (result.IsSanitized)
        {
            DatabaseSemanticConventionHelper.ApplyConventionsForSanitizedQueryText(
                activity,
                result.QueryText,
                result.QuerySummary,
                emitOldAttributes,
                emitNewAttributes);
        }
        else
        {
            DatabaseSemanticConventionHelper.ApplyConventionsForQueryText(
                activity,
                commandText,
                emitOldAttributes,
                emitNewAttributes);
        }
    }

    internal static DbQueryTextSanitizationResult SanitizeQueryText(DbQueryTextSanitizationContext context)
    {
        if (!EntityFrameworkProviderInfo.IsSqlLikeProvider(context.ProviderName))
        {
            return DbQueryTextSanitizationResult.NotSanitized;
        }

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(context.QueryText, EntityFrameworkProviderInfo.GetSqlDialect(context.ProviderName));

        return DbQueryTextSanitizationResult.Sanitized(sqlStatementInfo.SanitizedSql, sqlStatementInfo.DbQuerySummary);
    }
}
