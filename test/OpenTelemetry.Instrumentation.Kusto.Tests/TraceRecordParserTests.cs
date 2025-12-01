// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class TraceRecordParserTests
{
    [Fact]
    public void ParseRequestStartSuccess()
    {
        const string message = "$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://127.0.0.1:49902/v1/rest/message, DatabaseName=NetDefaultDB, App=testhost, User=REDMOND\\mattkot, ClientVersion=Kusto.Dotnet.Client:{14.0.2+b2d66614da1a4ff4561c5037c48e5be7002d66d4}|Runtime:{.NET_10.0.0/CLRv10.0.0/10.0.0-rtm.25523.111}, ClientRequestId=SW52YWxpZFRhYmxlIHwgdGFrZSAxMCB8IHdoZXJlIENvbDEgPSA3, text=InvalidTable | take 10 | where Col1=7 | summarize by Date, Time";
        var result = TraceRecordParser.ParseRequestStart(message);

        Assert.Equal("http://127.0.0.1:49902/v1/rest/message", result.Uri.ToString());
        Assert.Equal("127.0.0.1", result.ServerAddress.ToString());
        Assert.Equal("49902", result.ServerPort.ToString());
        Assert.Equal("NetDefaultDB", result.Database.ToString());
        Assert.Equal("InvalidTable | take 10 | where Col1=7 | summarize by Date, Time", result.QueryText.ToString());
    }

    [Fact]
    public void ParseRequestStartFailure()
    {
        const string message = "$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://";
        var result = TraceRecordParser.ParseRequestStart(message);

        Assert.Equal("http://", result.Uri.ToString());
        Assert.Equal(string.Empty, result.ServerAddress.ToString());
        Assert.Equal(string.Empty, result.ServerPort.ToString());
        Assert.Equal(string.Empty, result.Database.ToString());
        Assert.Equal(string.Empty, result.QueryText.ToString());
    }

    [Theory]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=TestDB, text=print 1", "localhost", null)]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://[2001:db8::1]:8080/v1/rest/query, DatabaseName=TestDB, text=print 1", "[2001:db8::1]", 8080)]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://[2001:db8::1]/v1/rest/query, DatabaseName=TestDB, text=print 1", "[2001:db8::1]", null)]
    public void ParseRequestStartServerAddressAndPort(string message, string expectedAddress, int? expectedPort)
    {
        var result = TraceRecordParser.ParseRequestStart(message);
        Assert.Equal(expectedAddress, result.ServerAddress.ToString());

        if (expectedPort.HasValue)
        {
            Assert.Equal(expectedPort.Value, result.ServerPort);
        }
        else
        {
            Assert.Null(result.ServerPort);
        }
    }

    [Fact]
    public void ParseActivityComplete()
    {
        const string message = "MonitoredActivityCompletedSuccessfully: ActivityType=KD.RestClient.ExecuteQuery, Timestamp=2025-12-01T02:30:30.0211167Z, ParentActivityId=52707aa6-de7f-42dd-adb9-bc3e6d976fa6, Duration=4316.802 [ms], HowEnded=Success";
        var result = TraceRecordParser.ParseActivityComplete(message);

        Assert.Equal("Success", result.HowEnded.ToString());
    }

    [Fact]
    public void ParseActivityCompleteFailure()
    {
        const string message = "MonitoredActivityCompletedSuccessfully: ActivityType=KD.RestClient.ExecuteQuery, Timestamp=2025-12-01T02:30:30.0211167Z, ParentActivityId=52707aa6-de7f-42dd-adb9-bc3e6d976fa6, Duration=4316.802 [ms]";
        var result = TraceRecordParser.ParseActivityComplete(message);

        Assert.Equal(string.Empty, result.HowEnded.ToString());
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
    }

    [Fact]
    public void ParseExceptionFailure()
    {
        const string message = "Exception object created: Kusto.Data.Exceptions.SemanticException Timestamp=2025-12-01T02:39:36.3878585Z";
        var result = TraceRecordParser.ParseException(message);

        Assert.Equal(string.Empty, result.ErrorMessage.ToString());
    }
}
