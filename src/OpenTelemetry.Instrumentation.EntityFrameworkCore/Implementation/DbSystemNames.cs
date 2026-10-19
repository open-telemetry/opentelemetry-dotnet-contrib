// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

/// <summary>
/// Known and used values for the <c>db.system.name</c> attribute.
/// </summary>
internal static class DbSystemNames
{
    public const string OtherSql = "other_sql";
    public const string AzureCosmosDb = "azure.cosmosdb";
    public const string Couchbase = "couchbase";
    public const string Firebirdsql = "firebirdsql";
    public const string GcpSpanner = "gcp.spanner";
    public const string IbmDb2 = "ibm.db2";
    public const string MicrosoftSqlServer = "microsoft.sql_server";
    public const string Mongodb = "mongodb";
    public const string Mysql = "mysql";
    public const string OracleDb = "oracle.db";
    public const string Postgresql = "postgresql";
    public const string Sqlite = "sqlite";
    public const string Teradata = "teradata";
}
