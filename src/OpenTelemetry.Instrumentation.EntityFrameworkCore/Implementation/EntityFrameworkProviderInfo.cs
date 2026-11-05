// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

/// <summary>
/// Provides database system and SQL dialect information for known Entity Framework Core providers.
/// </summary>
internal static class EntityFrameworkProviderInfo
{
    /// <summary>
    /// Gets the <c>db.system</c> and <c>db.system.name</c> values to use for the given provider or command name.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns>A tuple containing the respective <c>db.system</c> and <c>db.system.name</c> values.</returns>
    public static (string Old, string New) GetDbSystemNames(string? providerOrCommandName) =>
        //// "${Attribute} has the following list of well-known values. If one of them applies, then the respective value MUST be used"
        providerOrCommandName switch
        {
            //// These names are defined in the Semantic Conventions
            "Microsoft.Data.SqlClient.SqlCommand" or
            "Microsoft.EntityFrameworkCore.SqlServer"
                => (DbSystems.Mssql, DbSystemNames.MicrosoftSqlServer),
            "Microsoft.EntityFrameworkCore.Cosmos"
                => (DbSystems.Cosmosdb, DbSystemNames.AzureCosmosDb),
            "Devart.Data.SQLite.Entity.EFCore" or
            "Microsoft.Data.Sqlite.SqliteCommand" or
            "Microsoft.EntityFrameworkCore.Sqlite"
                => (DbSystems.Sqlite, DbSystemNames.Sqlite),
            "Devart.Data.MySql.Entity.EFCore" or
            "Devart.Data.MySql.MySqlCommand" or
            "MySql.Data.EntityFrameworkCore" or
            "MySql.Data.MySqlClient.MySqlCommand" or
            "MySql.EntityFrameworkCore" or
            "Pomelo.EntityFrameworkCore.MySql"
                => (DbSystems.Mysql, DbSystemNames.Mysql),
            "Npgsql.EntityFrameworkCore.PostgreSQL" or
            "Npgsql.NpgsqlCommand" or
            "Devart.Data.PostgreSql.Entity.EFCore" or
            "Devart.Data.PostgreSql.PgSqlCommand"
                => (DbSystems.Postgresql, DbSystemNames.Postgresql),
            "Oracle.EntityFrameworkCore" or
            "Oracle.ManagedDataAccess.Client.OracleCommand" or
            "Devart.Data.Oracle.Entity.EFCore" or
            "Devart.Data.Oracle.OracleCommand"
                => (DbSystems.Oracle, DbSystemNames.OracleDb),
            "FirebirdSql.Data.FirebirdClient.FbCommand" or
            "FirebirdSql.EntityFrameworkCore.Firebird"
                => (DbSystems.Firebird, DbSystemNames.Firebirdsql),
            "Google.Cloud.EntityFrameworkCore.Spanner" or
            "Google.Cloud.Spanner.Data.SpannerCommand"
                => (DbSystems.Spanner, DbSystemNames.GcpSpanner),
            "Teradata.Client.Provider.TdCommand" or
            "Teradata.EntityFrameworkCore"
                => (DbSystems.Teradata, DbSystemNames.Teradata),
            "MongoDB.EntityFrameworkCore"
                => (DbSystems.Mongodb, DbSystemNames.Mongodb),
            "Couchbase.EntityFrameworkCore" or
            "Couchbase.EntityFrameworkCore.Storage.Internal"
                => (DbSystems.Couchbase, DbSystemNames.Couchbase),
            "IBM.EntityFrameworkCore" or
            "IBM.EntityFrameworkCore-lnx" or
            "IBM.EntityFrameworkCore-osx"
                => (DbSystems.Db2, DbSystemNames.IbmDb2),
            //// Otherwise use the fallback defined in the Semantic Conventions
            _ => (DbSystems.OtherSql, DbSystemNames.OtherSql),
        };

    /// <summary>
    /// Returns whether the given provider or command name uses a SQL-like query language.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns><see langword="true"/> if the provider is SQL-like; otherwise, <see langword="false"/>.</returns>
    public static bool IsSqlLikeProvider(string? providerOrCommandName)
    {
        (_, var dbSystemName) = GetDbSystemNames(providerOrCommandName);

        return dbSystemName is
            DbSystemNames.Firebirdsql or
            DbSystemNames.GcpSpanner or
            DbSystemNames.IbmDb2 or
            DbSystemNames.MicrosoftSqlServer or
            DbSystemNames.Mysql or
            DbSystemNames.OracleDb or
            DbSystemNames.Postgresql or
            DbSystemNames.Sqlite or
            DbSystemNames.Teradata;
    }

    /// <summary>
    /// Returns whether the provider uses backslash escapes in SQL string literals by default.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns><see langword="true"/> for MySQL and Spanner providers; otherwise, <see langword="false"/>.</returns>
    public static bool IsBackslashEscapeProvider(string? providerOrCommandName)
    {
        // MySQL and MariaDB (which use the MySQL providers) treat a backslash as a
        // string-literal escape character unless the NO_BACKSLASH_ESCAPES SQL mode is enabled.
        // GoogleSQL (Spanner) does too, and like MySQL treats a double-quoted value as a
        // string literal and # as the start of a comment.
        (_, var dbSystemName) = GetDbSystemNames(providerOrCommandName);
        return dbSystemName is DbSystemNames.Mysql or DbSystemNames.GcpSpanner;
    }

    /// <summary>
    /// Returns whether the provider supports triple-quoted SQL string literals.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns><see langword="true"/> for Spanner providers; otherwise, <see langword="false"/>.</returns>
    public static bool IsTripleQuotedLiteralProvider(string? providerOrCommandName)
    {
        // GoogleSQL (Spanner) has triple-quoted string literals ('''...''' and """..."""), in which
        // a quote does not have to be escaped. In the other dialects ''' is a quote followed by an
        // escaped quote, so this must not be applied to them.
        (_, var dbSystemName) = GetDbSystemNames(providerOrCommandName);
        return dbSystemName == DbSystemNames.GcpSpanner;
    }

    /// <summary>
    /// Returns whether the provider only uses square brackets to delimit identifiers.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns><see langword="true"/> for SQL Server and SQLite providers; otherwise, <see langword="false"/>.</returns>
    public static bool IsBracketedIdentifierProvider(string? providerOrCommandName)
    {
        // In these databases [...] always delimits an identifier, which can have any character in it (e.g. [Mother's Name]).
        // In other dialects it can be a list of values (e.g. ['a', 'b']) or the subscript of an array (e.g. items[1]).
        (_, var dbSystemName) = GetDbSystemNames(providerOrCommandName);
        return dbSystemName is
            DbSystemNames.MicrosoftSqlServer or
            DbSystemNames.Sqlite;
    }

    /// <summary>
    /// Returns whether the provider allows block comments to be nested.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns><see langword="true"/> for SQL Server, PostgreSQL and Db2 providers; otherwise, <see langword="false"/>.</returns>
    public static bool IsNestedCommentProvider(string? providerOrCommandName)
    {
        // In these databases a comment such as /* /* */ text */ only ends at the second */. In the others
        // (e.g. SQLite, Oracle, Firebird, Teradata and MySQL) a block comment ends at the first */.
        (_, var dbSystemName) = GetDbSystemNames(providerOrCommandName);
        return dbSystemName is
            DbSystemNames.IbmDb2 or
            DbSystemNames.MicrosoftSqlServer or
            DbSystemNames.Postgresql;
    }
}
