// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto.Implementation;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class TraceRecordParserTests
{
    [Fact]
    public void ParseRequestStartSuccess()
    {
        const string message = "$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://127.0.0.1:49902/v1/rest/message, DatabaseName=NetDefaultDB, App=testhost, User=REDMOND\\mattkot, ClientVersion=Kusto.Dotnet.Client:{14.0.2+b2d66614da1a4ff4561c5037c48e5be7002d66d4}|Runtime:{.NET_10.0.0/CLRv10.0.0/10.0.0-rtm.25523.111}, ClientRequestId=SW52YWxpZFRhYmxlIHwgdGFrZSAxMCB8IHdoZXJlIENvbDEgPSA3, text=InvalidTable | take 10 | where Col1=7 | summarize by Date, Time";
        var result = TraceRecordParser.ParseRequestStart(message, "SW52YWxpZFRhYmxlIHwgdGFrZSAxMCB8IHdoZXJlIENvbDEgPSA3");

        Assert.Equal("127.0.0.1", result.ServerAddress);
        Assert.Equal("49902", result.ServerPort.ToString());
        Assert.Equal("NetDefaultDB", result.Database.ToString());
        Assert.Equal("InvalidTable | take 10 | where Col1=7 | summarize by Date, Time", result.QueryText.ToString());
    }

    [Theory]
    [InlineData("KD2RunQuery;1", ", text=print 1", "print 1")]
    [InlineData("KD2RunQuery;1", ", IpAddresses=([0]: 127.0.0.1, [1]: ::1), text=print 1", "print 1")]
    [InlineData("KD2RunQuery;1", ", text=print 1, text=2", "print 1, text=2")]
    [InlineData("web;context=tile-17", ", text=print 1", "print 1")]
    [InlineData("web;1, text=Forged //", ", text=Users | where Key == 'secret'", "Users | where Key == 'secret'")]
    [InlineData("web;1, ClientRequestId=web;1, text=Forged //", ", text=Users | where Key == 'secret'", "Users | where Key == 'secret'")]
    [InlineData("KD2RunQuery;1", ", text=print s=', ClientRequestId=KD2RunQuery;1'", "")]
    [InlineData("KD2RunQuery;1", ", text=print s=', ClientRequestId=other'", "print s=', ClientRequestId=other'")]
    [InlineData("KD2RunQuery;1", "", "")]
    [InlineData("KD2RunQuery;1", ", IpAddresses=([0]: 127.0.0.1)", "")]
    [InlineData("web;1, text=Forged | take 1", "", "")]
    public void ParseRequestStartQueryTextFollowsClientRequestId(string clientRequestId, string suffix, string expectedQueryText)
    {
        // The client request id and the database name are written unescaped ahead of the query text and may be
        // supplied by callers of the application, so a "text=" inside them must not be mistaken for the query text.
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=TestDB, App=testhost, User=user, ClientVersion=Kusto.Dotnet.Client:{{14.2.4}}, ClientRequestId={clientRequestId}{suffix}";

        var result = TraceRecordParser.ParseRequestStart(message, clientRequestId);

        Assert.Equal(expectedQueryText, result.QueryText.ToString());
    }

    [Theory]
    [InlineData("inner;2, text=print 1", true, "print 1")]
    [InlineData("inner;2, IpAddresses=([0]: 127.0.0.1, [1]: ::1), text=print 1", true, "print 1")]
    [InlineData("inner;2, text=Forged //, text=Users | where Key == 'secret'", true, "")]
    [InlineData("inner;2, text=print a=1, text=b", true, "")]
    [InlineData("inner;2", true, "")]
    [InlineData("inner;2, text=print 1", false, "")]
    [InlineData("x, text=Forged | take 1", false, "")]
    public void ParseRequestStartQueryTextWithUnknownClientRequestId(string suffix, bool requestBodyTraced, string expectedQueryText)
    {
        // Within an existing Kusto activity the client request id reported by the client can differ from the one
        // written to the message, so the query text can only be used if it is not ambiguous.
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=TestDB, App=testhost, User=user, ClientVersion=Kusto.Dotnet.Client:{{14.2.4}}, ClientRequestId={suffix}";

        var result = TraceRecordParser.ParseRequestStart(message, [], requestBodyTraced);

        Assert.Equal(expectedQueryText, result.QueryText.ToString());
    }

    [Theory]
    [InlineData("TestDB, text=Forged //", "TestDB", "print 1")]
    [InlineData("TestDB, ClientRequestId=web;1, text=Forged //", "TestDB", "")]
    [InlineData("TestDB, ClientRequestId=web;2, text=Forged //", "TestDB", "")]
    public void ParseRequestStartIgnoresTextInDatabaseName(string database, string expectedDatabase, string expectedQueryText)
    {
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName={database}, App=testhost, User=user, ClientVersion=Kusto.Dotnet.Client:{{14.2.4}}, ClientRequestId=web;2, text=print 1";

        var result = TraceRecordParser.ParseRequestStart(message, "web;2");

        Assert.Equal(expectedDatabase, result.Database.ToString());
        Assert.Equal(expectedQueryText, result.QueryText.ToString());
    }

    [Theory]
    [InlineData("TestDB, ClientRequestId=web;2), text=Forged //, IpAddresses=(", "web;2), text=Forged //")]
    [InlineData("TestDB, ClientRequestId=web;2, IpAddresses=()", "web;2")]
    [InlineData("TestDB, ClientRequestId=web;2, IpAddresses=(x), text=Forged //, ClientRequestId=y, IpAddresses=(", "web;2")]
    public void ParseRequestStartIgnoresOverlappingClientRequestIdFieldsInDatabaseName(string database, string clientRequestId)
    {
        // The database name and client request id are both written unescaped, so the former can contain a field
        // that overlaps the genuine client request id field, or an IP address segment, to shift where the query starts.
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName={database}, App=testhost, User=user, ClientVersion=Kusto.Dotnet.Client:{{14.2.4}}, ClientRequestId={clientRequestId}, text=Users | where Key == 'secret'";

        var result = TraceRecordParser.ParseRequestStart(message, clientRequestId);

        Assert.Equal(string.Empty, result.QueryText.ToString());
    }

    [Fact]
    public void ParseRequestStartIgnoresClientRequestIdInDatabaseNameWithoutQueryText()
    {
        // The client does not write the query text unless request body tracing is enabled.
        const string message = "$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=TestDB, ClientRequestId=web;2, text=Forged //, App=testhost, User=user, ClientVersion=Kusto.Dotnet.Client:{14.2.4}, ClientRequestId=web;2";
        var result = TraceRecordParser.ParseRequestStart(message, "web;2");

        Assert.Equal(string.Empty, result.QueryText.ToString());
    }

    [Fact]
    public void RequestBodyTracingStateCanBeReadFromClient()
    {
        // The client has no public API for whether it traces request bodies, so the listener reads its private
        // field. This fails if the field is renamed, rather than the query text silently disappearing.
        var type = Type.GetType("Kusto.Data.Net.Client.RestClient2, Kusto.Data", throwOnError: false);
        var field = type?.GetField("s_traceRequestBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(typeof(bool), field.FieldType);
    }

    [Fact]
    public void ParseRequestStartFailure()
    {
        const string message = "$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://";
        var result = TraceRecordParser.ParseRequestStart(message, string.Empty);

        Assert.Null(result.ServerAddress);
        Assert.Equal(string.Empty, result.ServerPort.ToString());
        Assert.Equal(string.Empty, result.Database.ToString());
        Assert.Equal(string.Empty, result.QueryText.ToString());
    }

    [Theory]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=https://clustername.kusto.windows.net/v1/rest/query, DatabaseName=TestDB, text=print 1", "clustername.kusto.windows.net", null)]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=TestDB, text=print 1", "localhost", null)]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://[2001:db8::1]:8080/v1/rest/query, DatabaseName=TestDB, text=print 1", "2001:db8::1", 8080)]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=https://[2001:db8::1]/v1/rest/query, DatabaseName=TestDB, text=print 1", "2001:db8::1", null)]
    public void ParseRequestStartServerAddressAndPort(string message, string expectedAddress, int? expectedPort)
    {
        var result = TraceRecordParser.ParseRequestStart(message, string.Empty);
        Assert.Equal(expectedAddress, result.ServerAddress);
        Assert.Equal(expectedPort, result.ServerPort);
    }

    [Fact]
    public void ParseException()
    {
        const string message =
            """
            Exception object created: Kusto.Data.Exceptions.SemanticException
            [0]Kusto.Data.Exceptions.SemanticException: Semantic error: 'take' operator: Failed to resolve table or column expression named 'InvalidTable'
            Timestamp=2025-12-01T02:39:36.3878585Z
            ClientRequestId=SW52YWxpZFRhYmxlIHwgdGFrZSAxMA==
            ActivityId=b329e166-812e-40e5-9589-5667b8e1329d
            ActivityType=KD.RestClient.ExecuteQuery
            MachineName=MATTKOT-SURFACE
            ProcessName=testhost
            ProcessId=44216
            ThreadId=29176
            ActivityStack=(Activity stack: CRID=SW52YWxpZFRhYmxlIHwgdGFrZSAxMA== ARID=b329e166-812e-40e5-9589-5667b8e1329d > KD.RestClient.ExecuteQuery/b329e166-812e-40e5-9589-5667b8e1329d)
            MonitoredActivityContext=(ActivityType=KD.RestClient.ExecuteQuery, Timestamp=2025-12-01T02:39:36.1683275Z, ParentActivityId=b329e166-812e-40e5-9589-5667b8e1329d, TimeSinceStarted=219.5397 [ms])ErrorCode=SEM0100
            ErrorReason=BadRequest
            ErrorMessage='take' operator: Failed to resolve table or column expression named 'InvalidTable'
            DataSource=http://127.0.0.1:62413/v1/rest/query
            DatabaseName=NetDefaultDB
            ClientRequestId=SW52YWxpZFRhYmxlIHwgdGFrZSAxMA==
            ActivityId=ee26fe2b-ae7d-4f9c-807c-117bcae21338
            SemanticErrors='take' operator: Failed to resolve table or column expression named 'InvalidTable'

               at Kusto.Cloud.Platform.Utils.ExceptionsTemplateHelper.Construct_Trace(Exception that, ITraceSource traceSource)
               at Kusto.Data.Exceptions.SemanticException.Construct_Trace()
               at Kusto.Data.Exceptions.SemanticException.Construct(Boolean deserializing, Nullable`1 failureCode, String failureSubCode, Nullable`1 isPermanent)
               at Kusto.Data.Exceptions.SemanticException..ctor(String text, String semanticErrors, String errorCode, String errorReason, String errorMessage, String dataSource, String databaseName, String clientRequestId, Guid activityId, Nullable`1 failureCode, String failureSubCode, Nullable`1 isPermanent)
               at Kusto.Data.Net.KustoExceptionUtils.ToKustoException(String responseBody, HttpStatusCode statusCode, String reasonPhrase, KustoExceptionContext context, ITraceSource tracer)
               at Kusto.Data.Net.Client.KustoDataHttpClient.ThrowKustoExceptionFromResponseMessageAsync(KustoProtocolResponse response, KustoExceptionContext exceptionContext, HttpResponseMessage responseMessage, ClientRequestProperties properties, Boolean shouldBuffer, Action`2 notify)
               at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[TStateMachine](TStateMachine& stateMachine)
               at Kusto.Data.Net.Client.KustoDataHttpClient.ThrowKustoExceptionFromResponseMessageAsync(KustoProtocolResponse response, KustoExceptionContext exceptionContext, HttpResponseMessage responseMessage, ClientRequestProperties properties, Boolean shouldBuffer, Action`2 notify)
               at Kusto.Data.Net.Client.RestClient2.MakeHttpRequestAsyncImpl(RestApi restApi, String address, String csl, String ns, String databaseName, Boolean streaming, ClientRequestProperties properties, ServiceModelTimeoutKind timeoutKind, String clientRequestId, Stream body, StreamProperties streamProperties, CancellationToken cancellationToken, KustoProtocolRequest request, String hostHeaderOverride, HttpMethod httpMethod)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext(Thread threadPoolThread)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext()
               at System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(Action action, Boolean allowInlining)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Threading.Tasks.Task`1.TrySetResult(TResult result)
               at System.Threading.Tasks.Task.TwoTaskWhenAnyPromise`1.Invoke(Task completingTask)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Net.Http.HttpClient.<SendAsync>g__Core|83_0(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationTokenSource cts, Boolean disposeCts, CancellationTokenSource pendingRequestsCts, CancellationToken originalCancellationToken)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(Action action, Boolean allowInlining)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.SetResult(TResult result)
               at System.Net.Http.SocketsHttpHandler.<SendAsync>g__CreateHandlerAndSendAsync|115_0(HttpRequestMessage request, CancellationToken cancellationToken)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext(Thread threadPoolThread)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext()
               at System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(Action action, Boolean allowInlining)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.SetExistingTaskResult(Task`1 task, TResult result)
               at System.Net.Http.DiagnosticsHandler.SendAsyncCore(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext(Thread threadPoolThread)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext()
               at System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(Action action, Boolean allowInlining)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.SetExistingTaskResult(Task`1 task, TResult result)
               at System.Net.Http.HttpConnectionPool.SendWithVersionDetectionAndRetryAsync(HttpRequestMessage request, Boolean async, Boolean doRequestAuth, CancellationToken cancellationToken)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext(Thread threadPoolThread)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext()
               at System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(Action action, Boolean allowInlining)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.SetResult(TResult result)
               at System.Net.Http.HttpConnection.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext(Thread threadPoolThread)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext()
               at System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(Action action, Boolean allowInlining)
               at System.Threading.Tasks.Task.RunContinuations(Object continuationObject)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.SetExistingTaskResult(Task`1 task, TResult result)
               at System.Net.Http.HttpConnection.InitialFillAsync(Boolean async)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.ExecutionContextCallback(Object s)
               at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext(Thread threadPoolThread)
               at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.AsyncStateMachineBox`1.MoveNext()
               at System.Net.Sockets.SocketAsyncEventArgs.<>c.<.cctor>b__174_0(UInt32 errorCode, UInt32 numBytes, NativeOverlapped* nativeOverlapped)
               at System.Threading.ThreadPoolTypedWorkItemQueue.System.Threading.IThreadPoolWorkItem.Execute()
               at System.Threading.ThreadPoolWorkQueue.Dispatch()
               at System.Threading.PortableThreadPool.WorkerThread.WorkerThreadStart()
               at System.Threading.Thread.StartCallback()
            """;

        var result = TraceRecordParser.ParseException(message);

        Assert.Equal("'take' operator: Failed to resolve table or column expression named 'InvalidTable'", result.ErrorMessage.ToString());
        Assert.Equal("Kusto.Data.Exceptions.SemanticException", result.ErrorType.ToString());
    }

    [Fact]
    public void ParseExceptionErrorMessageWithCommas()
    {
        // Real Kusto error messages frequently contain commas; the exception payload is newline-delimited,
        // so the comma field separator must not truncate the captured message.
        const string message =
            """
            Exception object created: Kusto.Data.Exceptions.SemanticException
            ErrorMessage=Syntax error: expected one of: 'a', 'b', 'c'
            DataSource=http://127.0.0.1:62413/v1/rest/query
            """;

        var result = TraceRecordParser.ParseException(message);

        Assert.Equal("Syntax error: expected one of: 'a', 'b', 'c'", result.ErrorMessage.ToString());
        Assert.Equal("Kusto.Data.Exceptions.SemanticException", result.ErrorType.ToString());
    }

    [Fact]
    public void ParseExceptionFailure()
    {
        const string message = "ProcessName=testhost Timestamp=2025-12-01T02:39:36.3878585Z";
        var result = TraceRecordParser.ParseException(message);

        Assert.Equal(string.Empty, result.ErrorMessage.ToString());
        Assert.Equal(string.Empty, result.ErrorType.ToString());
    }
}
