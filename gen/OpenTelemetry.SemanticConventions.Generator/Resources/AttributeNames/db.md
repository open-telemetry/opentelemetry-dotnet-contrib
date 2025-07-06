# db
| Id | Type | Description | Values | Brief | Stability | Reason |
|---|---|---|---|---|---|---|
| db.cassandra.consistency_level |  | Deprecated, use `cassandra.consistency.level` instead. | db.cassandra.consistency_level | | deprecated | Replaced by `cassandra.consistency.level`. |
| db.cassandra.coordinator.dc |  | Deprecated, use `cassandra.coordinator.dc` instead. | db.cassandra.coordinator.dc | | deprecated | Replaced by `cassandra.coordinator.dc`. |
| db.cassandra.coordinator.id |  | Deprecated, use `cassandra.coordinator.id` instead. | db.cassandra.coordinator.id | | deprecated | Replaced by `cassandra.coordinator.id`. |
| db.cassandra.idempotence |  | Deprecated, use `cassandra.query.idempotent` instead. | db.cassandra.idempotence | | deprecated | Replaced by `cassandra.query.idempotent`. |
| db.cassandra.page_size |  | Deprecated, use `cassandra.page.size` instead. | db.cassandra.page_size | | deprecated | Replaced by `cassandra.page.size`. |
| db.cassandra.speculative_execution_count |  | Deprecated, use `cassandra.speculative_execution.count` instead. | db.cassandra.speculative_execution_count | | deprecated | Replaced by `cassandra.speculative_execution.count`. |
| db.cassandra.table |  | Deprecated, use `db.collection.name` instead. | db.cassandra.table | | deprecated | Replaced by `db.collection.name`. |
| db.client.connection.pool.name |  | The name of the connection pool; unique within the instrumented application. In case the connection pool implementation doesn't provide a name, instrumentation SHOULD use a combination of parameters that would make the name unique, for example, combining attributes `server.address`, `server.port`, and `db.namespace`, formatted as `server.address:server.port/db.namespace`. Instrumentations that generate connection pool name following different patterns SHOULD document it. | db.client.connection.pool.name | | development |  |
| db.client.connection.state |  | The state of a connection in the pool | db.client.connection.state | | development |  |
| db.client.connections.pool.name |  | Deprecated, use `db.client.connection.pool.name` instead. | db.client.connections.pool.name | | deprecated | Replaced by `db.client.connection.pool.name`. |
| db.client.connections.state |  | Deprecated, use `db.client.connection.state` instead. | db.client.connections.state | | deprecated | Replaced by `db.client.connection.state`. |
| db.collection.name |  | The name of a collection (table, container) within the database. | db.collection.name | | stable |  |
| db.connection_string |  | Deprecated, use `server.address`, `server.port` attributes instead. | db.connection_string | | deprecated | Replaced by `server.address` and `server.port`. |
| db.cosmosdb.client_id |  | Deprecated, use `azure.client.id` instead. | db.cosmosdb.client_id | | deprecated | Replaced by `azure.client.id`. |
| db.cosmosdb.connection_mode |  | Deprecated, use `azure.cosmosdb.connection.mode` instead. | db.cosmosdb.connection_mode | | deprecated | Replaced by `azure.cosmosdb.connection.mode`. |
| db.cosmosdb.consistency_level |  | Deprecated, use `cosmosdb.consistency.level` instead. | db.cosmosdb.consistency_level | | deprecated | Replaced by `azure.cosmosdb.consistency.level`. |
| db.cosmosdb.container |  | Deprecated, use `db.collection.name` instead. | db.cosmosdb.container | | deprecated | Replaced by `db.collection.name`. |
| db.cosmosdb.operation_type |  | Deprecated, no replacement at this time. | db.cosmosdb.operation_type | | deprecated | No replacement at this time. |
| db.cosmosdb.regions_contacted |  | Deprecated, use `azure.cosmosdb.operation.contacted_regions` instead. | db.cosmosdb.regions_contacted | | deprecated | Replaced by `azure.cosmosdb.operation.contacted_regions`. |
| db.cosmosdb.request_charge |  | Deprecated, use `azure.cosmosdb.operation.request_charge` instead. | db.cosmosdb.request_charge | | deprecated | Replaced by `azure.cosmosdb.operation.request_charge`. |
| db.cosmosdb.request_content_length |  | Deprecated, use `azure.cosmosdb.request.body.size` instead. | db.cosmosdb.request_content_length | | deprecated | Replaced by `azure.cosmosdb.request.body.size`. |
| db.cosmosdb.status_code |  | Deprecated, use `db.response.status_code` instead. | db.cosmosdb.status_code | | deprecated | Replaced by `db.response.status_code`. |
| db.cosmosdb.sub_status_code |  | Deprecated, use `azure.cosmosdb.response.sub_status_code` instead. | db.cosmosdb.sub_status_code | | deprecated | Replaced by `azure.cosmosdb.response.sub_status_code`. |
| db.elasticsearch.cluster.name |  | Deprecated, use `db.namespace` instead. | db.elasticsearch.cluster.name | | deprecated | Replaced by `db.namespace`. |
| db.elasticsearch.node.name |  | Deprecated, use `elasticsearch.node.name` instead. | db.elasticsearch.node.name | | deprecated | Replaced by `elasticsearch.node.name`. |
| db.elasticsearch.path_parts |  | Deprecated, use `db.operation.parameter` instead. | db.elasticsearch.path_parts | | deprecated | Replaced by `db.operation.parameter`. |
| db.instance.id |  | Deprecated, no general replacement at this time. For Elasticsearch, use `db.elasticsearch.node.name` instead. | db.instance.id | | deprecated | Deprecated, no general replacement at this time. For Elasticsearch, use `db.elasticsearch.node.name` instead. |
| db.jdbc.driver_classname |  | Removed, no replacement at this time. | db.jdbc.driver_classname | | deprecated | Removed as not used. |
| db.mongodb.collection |  | Deprecated, use `db.collection.name` instead. | db.mongodb.collection | | deprecated | Replaced by `db.collection.name`. |
| db.mssql.instance_name |  | Deprecated, SQL Server instance is now populated as a part of `db.namespace` attribute. | db.mssql.instance_name | | deprecated | Deprecated, no replacement at this time. |
| db.name |  | Deprecated, use `db.namespace` instead. | db.name | | deprecated | Replaced by `db.namespace`. |
| db.namespace |  | The name of the database, fully qualified within the server address and port. | db.namespace | | stable |  |
| db.operation |  | Deprecated, use `db.operation.name` instead. | db.operation | | deprecated | Replaced by `db.operation.name`. |
| db.operation.batch.size |  | The number of queries included in a batch operation. | db.operation.batch.size | | stable |  |
| db.operation.name |  | The name of the operation or command being executed. | db.operation.name | | stable |  |
| db.operation.parameter |  | A database operation parameter, with `<key>` being the parameter name, and the attribute value being a string representation of the parameter value. | db.operation.parameter | | development |  |
| db.query.parameter |  | A database query parameter, with `<key>` being the parameter name, and the attribute value being a string representation of the parameter value. | db.query.parameter | | development |  |
| db.query.summary |  | Low cardinality summary of a database query. | db.query.summary | | stable |  |
| db.query.text |  | The database query being executed. | db.query.text | | stable |  |
| db.redis.database_index |  | Deprecated, use `db.namespace` instead. | db.redis.database_index | | deprecated | Replaced by `db.namespace`. |
| db.response.returned_rows |  | Number of rows returned by the operation. | db.response.returned_rows | | development |  |
| db.response.status_code |  | Database response status code. | db.response.status_code | | stable |  |
| db.sql.table |  | Deprecated, use `db.collection.name` instead, but only if not extracting the value from `db.query.text`. | db.sql.table | | deprecated | Replaced by `db.collection.name`, but only if not extracting the value from `db.query.text`. |
| db.statement |  | The database statement being executed. | db.statement | | deprecated | Replaced by `db.query.text`. |
| db.stored_procedure.name |  | The name of a stored procedure within the database. | db.stored_procedure.name | | stable |  |
| db.system |  | Deprecated, use `db.system.name` instead. | db.system | | deprecated | Replaced by `db.system.name`. |
| db.system.name |  | The database management system (DBMS) product as identified by the client instrumentation. | db.system.name | | stable |  |
| db.user |  | Deprecated, no replacement at this time. | db.user | | deprecated | No replacement at this time. |
