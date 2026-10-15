// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

/// <summary>
/// Applies Entity Framework Core query text sanitization and database attributes.
/// </summary>
internal static class EntityFrameworkQueryTextProcessor
{
    public static void Apply(
        Activity activity,
        object? command,
        string? commandText,
        string? providerName,
        EntityFrameworkInstrumentationOptions options)
    {
        if (!TrySanitizeQueryText(command, commandText, providerName, options, out var result))
        {
            return;
        }

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
                options.EmitOldAttributes,
                options.EmitNewAttributes);
            return;
        }

        DatabaseSemanticConventionHelper.ApplyConventionsForQueryText(
            activity,
            commandText,
            options.EmitOldAttributes,
            options.EmitNewAttributes,
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

    private static bool TrySanitizeQueryText(
        object? command,
        string? commandText,
        string? providerName,
        EntityFrameworkInstrumentationOptions options,
        out DbQueryTextSanitizationResult result)
    {
        try
        {
            result = options.QueryTextSanitizer?.Invoke(new DbQueryTextSanitizationContext(
                providerName,
                commandText,
                command as IDbCommand)) ?? DbQueryTextSanitizationResult.NotSanitized;
            return true;
        }
        catch (Exception ex)
        {
            EntityFrameworkInstrumentationEventSource.Log.QueryTextSanitizerException(ex);
            result = default;
            return false;
        }
    }
}
