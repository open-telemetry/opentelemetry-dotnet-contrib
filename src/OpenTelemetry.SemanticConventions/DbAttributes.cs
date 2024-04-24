// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from scripts/templates/SemanticConventionsAttributes.cs.j2</auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions
{
    /// <summary>
    /// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
    /// </summary>
    public static class DbAttributes
    {
        /// <summary>
        /// The consistency level of the query. Based on consistency values from <a href="https://docs.datastax.com/en/cassandra-oss/3.0/cassandra/dml/dmlConfigConsistency.html">CQL</a>.
        /// </summary>
        public const string AttributeDbCassandraConsistencyLevel = "db.cassandra.consistency_level";

        /// <summary>
        /// The data center of the coordinating node for a query.
        /// </summary>
        public const string AttributeDbCassandraCoordinatorDc = "db.cassandra.coordinator.dc";

        /// <summary>
        /// The ID of the coordinating node for a query.
        /// </summary>
        public const string AttributeDbCassandraCoordinatorId = "db.cassandra.coordinator.id";

        /// <summary>
        /// Whether or not the query is idempotent.
        /// </summary>
        public const string AttributeDbCassandraIdempotence = "db.cassandra.idempotence";

        /// <summary>
        /// The fetch size used for paging, i.e. how many rows will be returned at once.
        /// </summary>
        public const string AttributeDbCassandraPageSize = "db.cassandra.page_size";

        /// <summary>
        /// The number of times a query was speculatively executed. Not set or <c>0</c> if the query was not executed speculatively.
        /// </summary>
        public const string AttributeDbCassandraSpeculativeExecutionCount = "db.cassandra.speculative_execution_count";

        /// <summary>
        /// The name of the primary Cassandra table that the operation is acting upon, including the keyspace name (if applicable).
        /// </summary>
        /// <remarks>
        /// This mirrors the db.sql.table attribute but references cassandra rather than sql. It is not recommended to attempt any client-side parsing of <c>db.statement</c> just to get this property, but it should be set if it is provided by the library being instrumented. If the operation is acting upon an anonymous table, or more than one table, this value MUST NOT be set.
        /// </remarks>
        public const string AttributeDbCassandraTable = "db.cassandra.table";

        /// <summary>
        /// Deprecated, use <c>server.address</c>, <c>server.port</c> attributes instead.
        /// </summary>
        [Obsolete("Replaced by `server.address` and `server.port`.")]
        public const string AttributeDbConnectionString = "db.connection_string";

        /// <summary>
        /// Unique Cosmos client instance id.
        /// </summary>
        public const string AttributeDbCosmosdbClientId = "db.cosmosdb.client_id";

        /// <summary>
        /// Cosmos client connection mode.
        /// </summary>
        public const string AttributeDbCosmosdbConnectionMode = "db.cosmosdb.connection_mode";

        /// <summary>
        /// Cosmos DB container name.
        /// </summary>
        public const string AttributeDbCosmosdbContainer = "db.cosmosdb.container";

        /// <summary>
        /// CosmosDB Operation Type.
        /// </summary>
        public const string AttributeDbCosmosdbOperationType = "db.cosmosdb.operation_type";

        /// <summary>
        /// RU consumed for that operation.
        /// </summary>
        public const string AttributeDbCosmosdbRequestCharge = "db.cosmosdb.request_charge";

        /// <summary>
        /// Request payload size in bytes.
        /// </summary>
        public const string AttributeDbCosmosdbRequestContentLength = "db.cosmosdb.request_content_length";

        /// <summary>
        /// Cosmos DB status code.
        /// </summary>
        public const string AttributeDbCosmosdbStatusCode = "db.cosmosdb.status_code";

        /// <summary>
        /// Cosmos DB sub status code.
        /// </summary>
        public const string AttributeDbCosmosdbSubStatusCode = "db.cosmosdb.sub_status_code";

        /// <summary>
        /// Represents the identifier of an Elasticsearch cluster.
        /// </summary>
        public const string AttributeDbElasticsearchClusterName = "db.elasticsearch.cluster.name";

        /// <summary>
        /// Deprecated, use <c>db.instance.id</c> instead.
        /// </summary>
        [Obsolete("Replaced by `db.instance.id`")]
        public const string AttributeDbElasticsearchNodeName = "db.elasticsearch.node.name";

        /// <summary>
        /// A dynamic value in the url path.
        /// </summary>
        /// <remarks>
        /// Many Elasticsearch url paths allow dynamic values. These SHOULD be recorded in span attributes in the format <c>db.elasticsearch.path_parts.&lt;key&gt;</c>, where <c>&lt;key&gt;</c> is the url path part name. The implementation SHOULD reference the <a href="https://raw.githubusercontent.com/elastic/elasticsearch-specification/main/output/schema/schema.json">elasticsearch schema</a> in order to map the path part values to their names.
        /// </remarks>
        public const string AttributeDbElasticsearchPathParts = "db.elasticsearch.path_parts";

        /// <summary>
        /// An identifier (address, unique name, or any other identifier) of the database instance that is executing queries or mutations on the current connection. This is useful in cases where the database is running in a clustered environment and the instrumentation is able to record the node executing the query. The client may obtain this value in databases like MySQL using queries like <c>select @@hostname</c>.
        /// </summary>
        public const string AttributeDbInstanceId = "db.instance.id";

        /// <summary>
        /// Removed, no replacement at this time.
        /// </summary>
        [Obsolete("Removed as not used")]
        public const string AttributeDbJdbcDriverClassname = "db.jdbc.driver_classname";

        /// <summary>
        /// The MongoDB collection being accessed within the database stated in <c>db.name</c>.
        /// </summary>
        public const string AttributeDbMongodbCollection = "db.mongodb.collection";

        /// <summary>
        /// The Microsoft SQL Server <a href="https://docs.microsoft.com/sql/connect/jdbc/building-the-connection-url?view=sql-server-ver15">instance name</a> connecting to. This name is used to determine the port of a named instance.
        /// </summary>
        /// <remarks>
        /// If setting a <c>db.mssql.instance_name</c>, <c>server.port</c> is no longer required (but still recommended if non-standard).
        /// </remarks>
        public const string AttributeDbMssqlInstanceName = "db.mssql.instance_name";

        /// <summary>
        /// This attribute is used to report the name of the database being accessed. For commands that switch the database, this should be set to the target database (even if the command fails).
        /// </summary>
        /// <remarks>
        /// In some SQL databases, the database name to be used is called &amp;#34;schema name&amp;#34;. In case there are multiple layers that could be considered for database name (e.g. Oracle instance name and schema name), the database name to be used is the more specific layer (e.g. Oracle schema name).
        /// </remarks>
        public const string AttributeDbName = "db.name";

        /// <summary>
        /// The name of the operation being executed, e.g. the <a href="https://docs.mongodb.com/manual/reference/command/#database-operations">MongoDB command name</a> such as <c>findAndModify</c>, or the SQL keyword.
        /// </summary>
        /// <remarks>
        /// When setting this to an SQL keyword, it is not recommended to attempt any client-side parsing of <c>db.statement</c> just to get this property, but it should be set if the operation name is provided by the library being instrumented. If the SQL statement has an ambiguous operation, or performs more than one operation, this value may be omitted.
        /// </remarks>
        public const string AttributeDbOperation = "db.operation";

        /// <summary>
        /// The index of the database being accessed as used in the <a href="https://redis.io/commands/select"><c>SELECT</c> command</a>, provided as an integer. To be used instead of the generic <c>db.name</c> attribute.
        /// </summary>
        public const string AttributeDbRedisDatabaseIndex = "db.redis.database_index";

        /// <summary>
        /// The name of the primary table that the operation is acting upon, including the database name (if applicable).
        /// </summary>
        /// <remarks>
        /// It is not recommended to attempt any client-side parsing of <c>db.statement</c> just to get this property, but it should be set if it is provided by the library being instrumented. If the operation is acting upon an anonymous table, or more than one table, this value MUST NOT be set.
        /// </remarks>
        public const string AttributeDbSqlTable = "db.sql.table";

        /// <summary>
        /// The database statement being executed.
        /// </summary>
        public const string AttributeDbStatement = "db.statement";

        /// <summary>
        /// An identifier for the database management system (DBMS) product being used. See below for a list of well-known identifiers.
        /// </summary>
        public const string AttributeDbSystem = "db.system";

        /// <summary>
        /// Username for accessing the database.
        /// </summary>
        public const string AttributeDbUser = "db.user";

        /// <summary>
        /// The consistency level of the query. Based on consistency values from <a href="https://docs.datastax.com/en/cassandra-oss/3.0/cassandra/dml/dmlConfigConsistency.html">CQL</a>.
        /// </summary>
        public static class DbCassandraConsistencyLevelValues
        {
            /// <summary>
            /// all.
            /// </summary>
            public const string All = "all";

            /// <summary>
            /// each_quorum.
            /// </summary>
            public const string EachQuorum = "each_quorum";

            /// <summary>
            /// quorum.
            /// </summary>
            public const string Quorum = "quorum";

            /// <summary>
            /// local_quorum.
            /// </summary>
            public const string LocalQuorum = "local_quorum";

            /// <summary>
            /// one.
            /// </summary>
            public const string One = "one";

            /// <summary>
            /// two.
            /// </summary>
            public const string Two = "two";

            /// <summary>
            /// three.
            /// </summary>
            public const string Three = "three";

            /// <summary>
            /// local_one.
            /// </summary>
            public const string LocalOne = "local_one";

            /// <summary>
            /// any.
            /// </summary>
            public const string Any = "any";

            /// <summary>
            /// serial.
            /// </summary>
            public const string Serial = "serial";

            /// <summary>
            /// local_serial.
            /// </summary>
            public const string LocalSerial = "local_serial";
        }

        /// <summary>
        /// Cosmos client connection mode.
        /// </summary>
        public static class DbCosmosdbConnectionModeValues
        {
            /// <summary>
            /// Gateway (HTTP) connections mode.
            /// </summary>
            public const string Gateway = "gateway";

            /// <summary>
            /// Direct connection.
            /// </summary>
            public const string Direct = "direct";
        }

        /// <summary>
        /// CosmosDB Operation Type.
        /// </summary>
        public static class DbCosmosdbOperationTypeValues
        {
            /// <summary>
            /// invalid.
            /// </summary>
            public const string Invalid = "Invalid";

            /// <summary>
            /// create.
            /// </summary>
            public const string Create = "Create";

            /// <summary>
            /// patch.
            /// </summary>
            public const string Patch = "Patch";

            /// <summary>
            /// read.
            /// </summary>
            public const string Read = "Read";

            /// <summary>
            /// read_feed.
            /// </summary>
            public const string ReadFeed = "ReadFeed";

            /// <summary>
            /// delete.
            /// </summary>
            public const string Delete = "Delete";

            /// <summary>
            /// replace.
            /// </summary>
            public const string Replace = "Replace";

            /// <summary>
            /// execute.
            /// </summary>
            public const string Execute = "Execute";

            /// <summary>
            /// query.
            /// </summary>
            public const string Query = "Query";

            /// <summary>
            /// head.
            /// </summary>
            public const string Head = "Head";

            /// <summary>
            /// head_feed.
            /// </summary>
            public const string HeadFeed = "HeadFeed";

            /// <summary>
            /// upsert.
            /// </summary>
            public const string Upsert = "Upsert";

            /// <summary>
            /// batch.
            /// </summary>
            public const string Batch = "Batch";

            /// <summary>
            /// query_plan.
            /// </summary>
            public const string QueryPlan = "QueryPlan";

            /// <summary>
            /// execute_javascript.
            /// </summary>
            public const string ExecuteJavascript = "ExecuteJavaScript";
        }

        /// <summary>
        /// An identifier for the database management system (DBMS) product being used. See below for a list of well-known identifiers.
        /// </summary>
        public static class DbSystemValues
        {
            /// <summary>
            /// Some other SQL database. Fallback only. See notes.
            /// </summary>
            public const string OtherSql = "other_sql";

            /// <summary>
            /// Microsoft SQL Server.
            /// </summary>
            public const string Mssql = "mssql";

            /// <summary>
            /// Microsoft SQL Server Compact.
            /// </summary>
            public const string Mssqlcompact = "mssqlcompact";

            /// <summary>
            /// MySQL.
            /// </summary>
            public const string Mysql = "mysql";

            /// <summary>
            /// Oracle Database.
            /// </summary>
            public const string Oracle = "oracle";

            /// <summary>
            /// IBM Db2.
            /// </summary>
            public const string Db2 = "db2";

            /// <summary>
            /// PostgreSQL.
            /// </summary>
            public const string Postgresql = "postgresql";

            /// <summary>
            /// Amazon Redshift.
            /// </summary>
            public const string Redshift = "redshift";

            /// <summary>
            /// Apache Hive.
            /// </summary>
            public const string Hive = "hive";

            /// <summary>
            /// Cloudscape.
            /// </summary>
            public const string Cloudscape = "cloudscape";

            /// <summary>
            /// HyperSQL DataBase.
            /// </summary>
            public const string Hsqldb = "hsqldb";

            /// <summary>
            /// Progress Database.
            /// </summary>
            public const string Progress = "progress";

            /// <summary>
            /// SAP MaxDB.
            /// </summary>
            public const string Maxdb = "maxdb";

            /// <summary>
            /// SAP HANA.
            /// </summary>
            public const string Hanadb = "hanadb";

            /// <summary>
            /// Ingres.
            /// </summary>
            public const string Ingres = "ingres";

            /// <summary>
            /// FirstSQL.
            /// </summary>
            public const string Firstsql = "firstsql";

            /// <summary>
            /// EnterpriseDB.
            /// </summary>
            public const string Edb = "edb";

            /// <summary>
            /// InterSystems Cach&#233;.
            /// </summary>
            public const string Cache = "cache";

            /// <summary>
            /// Adabas (Adaptable Database System).
            /// </summary>
            public const string Adabas = "adabas";

            /// <summary>
            /// Firebird.
            /// </summary>
            public const string Firebird = "firebird";

            /// <summary>
            /// Apache Derby.
            /// </summary>
            public const string Derby = "derby";

            /// <summary>
            /// FileMaker.
            /// </summary>
            public const string Filemaker = "filemaker";

            /// <summary>
            /// Informix.
            /// </summary>
            public const string Informix = "informix";

            /// <summary>
            /// InstantDB.
            /// </summary>
            public const string Instantdb = "instantdb";

            /// <summary>
            /// InterBase.
            /// </summary>
            public const string Interbase = "interbase";

            /// <summary>
            /// MariaDB.
            /// </summary>
            public const string Mariadb = "mariadb";

            /// <summary>
            /// Netezza.
            /// </summary>
            public const string Netezza = "netezza";

            /// <summary>
            /// Pervasive PSQL.
            /// </summary>
            public const string Pervasive = "pervasive";

            /// <summary>
            /// PointBase.
            /// </summary>
            public const string Pointbase = "pointbase";

            /// <summary>
            /// SQLite.
            /// </summary>
            public const string Sqlite = "sqlite";

            /// <summary>
            /// Sybase.
            /// </summary>
            public const string Sybase = "sybase";

            /// <summary>
            /// Teradata.
            /// </summary>
            public const string Teradata = "teradata";

            /// <summary>
            /// Vertica.
            /// </summary>
            public const string Vertica = "vertica";

            /// <summary>
            /// H2.
            /// </summary>
            public const string H2 = "h2";

            /// <summary>
            /// ColdFusion IMQ.
            /// </summary>
            public const string Coldfusion = "coldfusion";

            /// <summary>
            /// Apache Cassandra.
            /// </summary>
            public const string Cassandra = "cassandra";

            /// <summary>
            /// Apache HBase.
            /// </summary>
            public const string Hbase = "hbase";

            /// <summary>
            /// MongoDB.
            /// </summary>
            public const string Mongodb = "mongodb";

            /// <summary>
            /// Redis.
            /// </summary>
            public const string Redis = "redis";

            /// <summary>
            /// Couchbase.
            /// </summary>
            public const string Couchbase = "couchbase";

            /// <summary>
            /// CouchDB.
            /// </summary>
            public const string Couchdb = "couchdb";

            /// <summary>
            /// Microsoft Azure Cosmos DB.
            /// </summary>
            public const string Cosmosdb = "cosmosdb";

            /// <summary>
            /// Amazon DynamoDB.
            /// </summary>
            public const string Dynamodb = "dynamodb";

            /// <summary>
            /// Neo4j.
            /// </summary>
            public const string Neo4j = "neo4j";

            /// <summary>
            /// Apache Geode.
            /// </summary>
            public const string Geode = "geode";

            /// <summary>
            /// Elasticsearch.
            /// </summary>
            public const string Elasticsearch = "elasticsearch";

            /// <summary>
            /// Memcached.
            /// </summary>
            public const string Memcached = "memcached";

            /// <summary>
            /// CockroachDB.
            /// </summary>
            public const string Cockroachdb = "cockroachdb";

            /// <summary>
            /// OpenSearch.
            /// </summary>
            public const string Opensearch = "opensearch";

            /// <summary>
            /// ClickHouse.
            /// </summary>
            public const string Clickhouse = "clickhouse";

            /// <summary>
            /// Cloud Spanner.
            /// </summary>
            public const string Spanner = "spanner";

            /// <summary>
            /// Trino.
            /// </summary>
            public const string Trino = "trino";
        }
    }
}
