// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

/// <summary>
/// Known and used values for the <c>db.system</c> attribute.
/// </summary>
internal static class DbSystems
{
    public const string OtherSql = "other_sql";
    public const string Cosmosdb = "cosmosdb";
    public const string Couchbase = "couchbase";
    public const string Db2 = "db2";
    public const string Firebird = "firebird";
    public const string Mongodb = "mongodb";
    public const string Mssql = "mssql";
    public const string Mysql = "mysql";
    public const string Oracle = "oracle";
    public const string Postgresql = "postgresql";
    public const string Spanner = "spanner";
    public const string Sqlite = "sqlite";
    public const string Teradata = "teradata";
}
