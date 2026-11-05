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
    /// Gets the dialect of SQL which the provider uses, for the sanitization of query text.
    /// </summary>
    /// <param name="providerOrCommandName">The provider or command name.</param>
    /// <returns>The dialect of the provider.</returns>
    public static SqlProcessor.SqlDialect GetSqlDialect(string? providerOrCommandName)
    {
        (_, var dbSystemName) = GetDbSystemNames(providerOrCommandName);

        return dbSystemName switch
        {
            DbSystemNames.Mysql => SqlProcessor.SqlDialect.MySql,
            DbSystemNames.GcpSpanner => SqlProcessor.SqlDialect.GoogleSql,
            DbSystemNames.MicrosoftSqlServer => SqlProcessor.SqlDialect.SqlServer,
            DbSystemNames.Sqlite => SqlProcessor.SqlDialect.Sqlite,

            DbSystemNames.OracleDb => SqlProcessor.SqlDialect.Oracle,
            DbSystemNames.Firebirdsql => SqlProcessor.SqlDialect.Firebird,
            DbSystemNames.Teradata => SqlProcessor.SqlDialect.Teradata,
            _ => SqlProcessor.SqlDialect.Default,
        };
    }
}
