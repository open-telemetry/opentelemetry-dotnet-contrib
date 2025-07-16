# db
| Id | Type | Description | Values | Brief | Stability | Reason |
|---|---|---|---|---|---|---|
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | all | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | each_quorum | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | quorum | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | local_quorum | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | one | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | two | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | three | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | local_one | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | any | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | serial | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | local_serial | none | deprecated | Replaced by `cassandra.consistency.level`. |
| db.client.connection.state |  | The state of a connection in the pool | idle | none | development |  |
| db.client.connection.state |  | The state of a connection in the pool | used | none | development |  |
| db.client.connections.state |  | Deprecated, use `db.client.connection.state` instead. | idle | none | deprecated | Replaced by `db.client.connection.state`. |
| db.client.connections.state |  | Deprecated, use `db.client.connection.state` instead. | used | none | deprecated | Replaced by `db.client.connection.state`. |
| db.cosmosdb.connection_mode |  | Deprecated, use `azure.cosmosdb.connection.mode` instead. | gateway | Gateway (HTTP) connection. | deprecated | Replaced by `azure.cosmosdb.connection.mode`. |
| db.cosmosdb.connection_mode |  | Deprecated, use `azure.cosmosdb.connection.mode` instead. | direct | Direct connection. | deprecated | Replaced by `azure.cosmosdb.connection.mode`. |
| db.cosmosdb.consistency_level |  | Deprecated, use `cosmosdb.consistency.level` instead. | Strong | none | deprecated | Replaced by `azure.cosmosdb.consistency.level`. |
| db.cosmosdb.consistency_level |  | Deprecated, use `cosmosdb.consistency.level` instead. | BoundedStaleness | none | deprecated | Replaced by `azure.cosmosdb.consistency.level`. |
| db.cosmosdb.consistency_level |  | Deprecated, use `cosmosdb.consistency.level` instead. | Session | none | deprecated | Replaced by `azure.cosmosdb.consistency.level`. |
| db.cosmosdb.consistency_level |  | Deprecated, use `cosmosdb.consistency.level` instead. | Eventual | none | deprecated | Replaced by `azure.cosmosdb.consistency.level`. |
| db.cosmosdb.consistency_level |  | Deprecated, use `cosmosdb.consistency.level` instead. | ConsistentPrefix | none | deprecated | Replaced by `azure.cosmosdb.consistency.level`. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | batch | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | create | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | delete | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | execute | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | execute_javascript | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | invalid | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | head | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | head_feed | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | patch | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | query | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | query_plan | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | read | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | read_feed | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | replace | none | deprecated | No replacement at this time. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | upsert | none | deprecated | No replacement at this time. |
| db.system |  | Deprecated, use `db.system.name` instead. | other_sql | Some other SQL database. Fallback only. See notes. | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | adabas | Adabas (Adaptable Database System) | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | cache | Deprecated, use `intersystems_cache` instead. | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | intersystems_cache | InterSystems Caché | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | cassandra | Apache Cassandra | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | clickhouse | ClickHouse | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | cloudscape | Deprecated, use `other_sql` instead. | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | cockroachdb | CockroachDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | coldfusion | Deprecated, no replacement at this time. | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | cosmosdb | Microsoft Azure Cosmos DB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | couchbase | Couchbase | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | couchdb | CouchDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | db2 | IBM Db2 | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | derby | Apache Derby | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | dynamodb | Amazon DynamoDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | edb | EnterpriseDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | elasticsearch | Elasticsearch | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | filemaker | FileMaker | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | firebird | Firebird | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | firstsql | Deprecated, use `other_sql` instead. | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | geode | Apache Geode | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | h2 | H2 | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | hanadb | SAP HANA | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | hbase | Apache HBase | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | hive | Apache Hive | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | hsqldb | HyperSQL DataBase | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | influxdb | InfluxDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | informix | Informix | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | ingres | Ingres | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | instantdb | InstantDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | interbase | InterBase | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | mariadb | MariaDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | maxdb | SAP MaxDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | memcached | Memcached | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | mongodb | MongoDB | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | mssql | Microsoft SQL Server | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | mssqlcompact | Deprecated, Microsoft SQL Server Compact is discontinued. | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | mysql | MySQL | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | neo4j | Neo4j | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | netezza | Netezza | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | opensearch | OpenSearch | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | oracle | Oracle Database | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | pervasive | Pervasive PSQL | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | pointbase | PointBase | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | postgresql | PostgreSQL | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | progress | Progress Database | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | redis | Redis | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | redshift | Amazon Redshift | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | spanner | Cloud Spanner | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | sqlite | SQLite | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | sybase | Sybase | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | teradata | Teradata | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | trino | Trino | deprecated | Replaced by `db.system.name`. |
| db.system |  | Deprecated, use `db.system.name` instead. | vertica | Vertica | deprecated | Replaced by `db.system.name`. |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | other_sql | Some other SQL database. Fallback only. | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | softwareag.adabas | [Adabas (Adaptable Database System)](https://documentation.softwareag.com/?pf=adabas) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | actian.ingres | [Actian Ingres](https://www.actian.com/databases/ingres/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | aws.dynamodb | [Amazon DynamoDB](https://aws.amazon.com/pm/dynamodb/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | aws.redshift | [Amazon Redshift](https://aws.amazon.com/redshift/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | azure.cosmosdb | [Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | intersystems.cache | [InterSystems Caché](https://www.intersystems.com/products/cache/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | cassandra | [Apache Cassandra](https://cassandra.apache.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | clickhouse | [ClickHouse](https://clickhouse.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | cockroachdb | [CockroachDB](https://www.cockroachlabs.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | couchbase | [Couchbase](https://www.couchbase.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | couchdb | [Apache CouchDB](https://couchdb.apache.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | derby | [Apache Derby](https://db.apache.org/derby/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | elasticsearch | [Elasticsearch](https://www.elastic.co/elasticsearch) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | firebirdsql | [Firebird](https://www.firebirdsql.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | gcp.spanner | [Google Cloud Spanner](https://cloud.google.com/spanner) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | geode | [Apache Geode](https://geode.apache.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | h2database | [H2 Database](https://h2database.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | hbase | [Apache HBase](https://hbase.apache.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | hive | [Apache Hive](https://hive.apache.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | hsqldb | [HyperSQL Database](https://hsqldb.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | ibm.db2 | [IBM Db2](https://www.ibm.com/db2) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | ibm.informix | [IBM Informix](https://www.ibm.com/products/informix) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | ibm.netezza | [IBM Netezza](https://www.ibm.com/products/netezza) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | influxdb | [InfluxDB](https://www.influxdata.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | instantdb | [Instant](https://www.instantdb.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | mariadb | [MariaDB](https://mariadb.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | memcached | [Memcached](https://memcached.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | mongodb | [MongoDB](https://www.mongodb.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | microsoft.sql_server | [Microsoft SQL Server](https://www.microsoft.com/sql-server) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | mysql | [MySQL](https://www.mysql.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | neo4j | [Neo4j](https://neo4j.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | opensearch | [OpenSearch](https://opensearch.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | oracle.db | [Oracle Database](https://www.oracle.com/database/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | postgresql | [PostgreSQL](https://www.postgresql.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | redis | [Redis](https://redis.io/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | sap.hana | [SAP HANA](https://www.sap.com/products/technology-platform/hana/what-is-sap-hana.html) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | sap.maxdb | [SAP MaxDB](https://maxdb.sap.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | sqlite | [SQLite](https://www.sqlite.org/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | teradata | [Teradata](https://www.teradata.com/) | stable |  |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | trino | [Trino](https://trino.io/) | stable |  |
