# StackExchangeRedis Instrumentation for OpenTelemetry .NET - Example

An example application that shows how to use
`OpenTelemetry.Instrumentation.StackExchangeRedis` to capture traces of outgoing
calls to Redis. You should see the following output on the Console when you run
this application (please look at the prerequisite for running the application in
[Program.cs](./Program.cs):

```text
Activity.TraceId:          f1a47baec558ebb97e57a6fb7d029a29
Activity.SpanId:           d0abf2503ad3d1b6
Activity.TraceFlags:           Recorded
Activity.ActivitySourceName: OpenTelemetry.StackExchange.Redis
Activity.DisplayName: SET
Activity.Kind:        Client
Activity.StartTime:   2022-06-06T22:17:40.8927802Z
Activity.Duration:    00:00:00.0237767
Activity.Tags:
    db.system: redis
    db.redis.flags: DemandMaster
    db.statement: SET
    net.peer.name: localhost
    net.peer.port: 6379
    db.redis.database_index: 0
   StatusCode : UNSET
Activity.Events:
    Enqueued [6/6/2022 10:17:40 PM +00:00]
    Sent [6/6/2022 10:17:40 PM +00:00]
    ResponseReceived [6/6/2022 10:17:40 PM +00:00]
Resource associated with Activity:
    service.name: unknown_service:Examples.StackExchangeRedis

Activity.TraceId:          0db37d796826693e23b17e3b082356a4
Activity.SpanId:           cd01d518b8b5cfa2
Activity.TraceFlags:           Recorded
Activity.ActivitySourceName: OpenTelemetry.StackExchange.Redis
Activity.DisplayName: GET
Activity.Kind:        Client
Activity.StartTime:   2022-06-06T22:17:41.9223474Z
Activity.Duration:    00:00:00.0067062
Activity.Tags:
    db.system: redis
    db.redis.flags: None
    db.statement: GET
    net.peer.name: localhost
    net.peer.port: 6379
    db.redis.database_index: 0
   StatusCode : UNSET
Activity.Events:
    Enqueued [6/6/2022 10:17:41 PM +00:00]
    Sent [6/6/2022 10:17:41 PM +00:00]
    ResponseReceived [6/6/2022 10:17:41 PM +00:00]
Resource associated with Activity:
    service.name: unknown_service:Examples.StackExchangeRedis
```
