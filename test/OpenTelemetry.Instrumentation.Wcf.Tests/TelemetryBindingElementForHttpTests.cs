// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.ServiceModel;
using OpenTelemetry.Instrumentation.Wcf.Tests.Tools;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class TelemetryBindingElementForHttpTests : IClassFixture<WeaverFixture>, IDisposable
{
    private readonly Uri serviceBaseUri;
    private readonly HttpListener listener;
    private readonly Task listenerTask;
    private readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ITestOutputHelper output;
    private readonly WeaverFixture weaver;

    public TelemetryBindingElementForHttpTests(WeaverFixture weaver, ITestOutputHelper outputHelper)
    {
        this.output = outputHelper;
        this.weaver = weaver;

        var retryCount = WcfTestHelpers.MaxRetries;
        HttpListener? createdListener = null;
        while (retryCount > 0)
        {
            try
            {
                this.serviceBaseUri = WcfTestHelpers.GetRandomBaseUri(Uri.UriSchemeHttp);

                createdListener = new HttpListener();
                createdListener.Prefixes.Add(this.serviceBaseUri.OriginalString);
                createdListener.Start();
                break;
            }
            catch
            {
                createdListener?.Close();
                createdListener = null;
                retryCount--;
            }
        }

        if (createdListener == null || this.serviceBaseUri == null)
        {
            throw new InvalidOperationException("HttpListener could not be started.");
        }

        this.listener = createdListener;
        this.listenerTask = Task.Run(this.ListenAsync);
        this.initialized.Task.Wait();
    }

    public static TheoryData<bool, bool, bool, bool, bool, bool, bool, bool, bool> IncomingRequestTestData()
    {
        var testCases = new TheoryData<bool, bool, bool, bool, bool, bool, bool, bool, bool>();

        bool[] booleans = [false, true];

        foreach (var emitOldAttributes in booleans)
        {
            foreach (var emitNewAttributes in booleans)
            {
                if (!emitOldAttributes && !emitNewAttributes)
                {
                    // Invalid combination - at least one must be true
                    continue;
                }

                testCases.Add(emitOldAttributes, emitNewAttributes, false, false, false, false, false, false, false);
                testCases.Add(emitOldAttributes, emitNewAttributes, true, false, false, false, false, false, false);
                testCases.Add(emitOldAttributes, emitNewAttributes, true, true, false, false, false, false, false);
                testCases.Add(emitOldAttributes, emitNewAttributes, true, false, true, true, false, false, false);
                testCases.Add(emitOldAttributes, emitNewAttributes, true, false, true, true, true, false, false);
                testCases.Add(emitOldAttributes, emitNewAttributes, true, false, true, true, true, true, false);
                testCases.Add(emitOldAttributes, emitNewAttributes, true, false, true, true, true, true, true);
            }
        }

        return testCases;
    }

    public void Dispose()
    {
        try
        {
            this.listener.Close();
            this.listenerTask.Wait();
        }
        catch (Exception ex) when (this.IsListenerShutdownException(ex))
        {
            // Listener was already closed as part of disposal.
        }
    }

    [Theory]
    [MemberData(nameof(IncomingRequestTestData))]
    public async Task OutgoingRequestInstrumentationTest(
        bool emitOldAttributes,
        bool emitNewAttributes,
        bool instrument,
        bool filter,
        bool suppressDownstreamInstrumentation,
        bool includeVersion,
        bool enrich,
        bool enrichmentException,
        bool emptyOrNullAction)
    {
        using var scope = SemanticConventionScope.Get(emitOldAttributes, emitNewAttributes);

        List<Activity> stoppedActivities = [];

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities);

        if (instrument)
        {
            builder
                .AddWcfInstrumentation(options =>
                {
                    if (enrich)
                    {
                        options.Enrich = enrichmentException
                            ? (_, _, _) => throw new Exception("Error while enriching activity")
                            : (activity, eventName, _) =>
                            {
                                switch (eventName)
                                {
                                    case WcfEnrichEventNames.BeforeSendRequest:
                                        activity.SetTag("client.beforesendrequest", WcfEnrichEventNames.BeforeSendRequest);
                                        break;
                                    case WcfEnrichEventNames.AfterReceiveReply:
                                        activity.SetTag("client.afterreceivereply", WcfEnrichEventNames.AfterReceiveReply);
                                        break;
                                    default:
                                        break;
                                }
                            };
                    }

                    options.OutgoingRequestFilter = _ => !filter;
                    options.SuppressDownstreamInstrumentation = suppressDownstreamInstrumentation;
                    options.SetSoapMessageVersion = includeVersion;
                })
                .AddDownstreamInstrumentation();
        }

        var tracerProvider = builder.Build();

        var client = new ServiceClient(
            new BasicHttpBinding(BasicHttpSecurityMode.None),
            new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));

        try
        {
            client.Endpoint.EndpointBehaviors.Add(new DownstreamInstrumentationEndpointBehavior());
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            var req = new ServiceRequest(payload: "Hello Open Telemetry!");

            if (emptyOrNullAction)
            {
                await client.ExecuteWithEmptyActionNameAsync(req);
            }
            else
            {
                await client.ExecuteAsync(req);
            }
        }
        finally
        {
            client.AbortOrClose();
            tracerProvider?.Shutdown();
            tracerProvider?.Dispose();

            WcfInstrumentationActivitySource.Options = null;
        }

        if (instrument)
        {
            if (!suppressDownstreamInstrumentation)
            {
                WcfTestHelpers.AssertDownstreamInstrumentationActivities(stoppedActivities, filter);
            }
            else
            {
                if (!filter)
                {
                    Assert.NotEmpty(stoppedActivities);
                    var activity = Assert.Single(stoppedActivities);

                    WcfTestHelpers.AssertOutgoingRequestActivity(
                        activity,
                        this.serviceBaseUri,
                        emptyOrNullAction,
                        includeVersion,
                        "Soap11 (http://schemas.xmlsoap.org/soap/envelope/) AddressingNone (http://schemas.microsoft.com/ws/2005/05/addressing/none)",
                        "http",
                        enrich,
                        enrichmentException,
                        emitOldAttributes,
                        emitNewAttributes);

                    if (emitNewAttributes && !emitOldAttributes && !enrich && DockerHelper.IsAvailable(DockerPlatform.Linux))
                    {
                        await WeaverTelemetryVerifier.VerifyAsync(
                            (stoppedActivities, []),
                            WcfInstrumentationActivitySource.SemanticConventionsVersionNew,
                            this.weaver,
                            this.output,
                            WcfTestHelpers.WeaverSuppressions);
                    }
                }
                else
                {
                    Assert.Empty(stoppedActivities);
                }
            }
        }
        else
        {
            Assert.Empty(stoppedActivities);
        }
    }

    [Fact]
    public async Task ActivitiesHaveCorrectParentTest()
    {
        var testSource = new ActivitySource("TestSource");

        List<Activity> stoppedActivities = [];
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var client = new ServiceClient(
            new BasicHttpBinding(BasicHttpSecurityMode.None),
            new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
        try
        {
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

            using var parentActivity = testSource.StartActivity("ParentActivity");
            client.ExecuteSynchronous(new ServiceRequest(payload: "Hello Open Telemetry!"));
            client.ExecuteSynchronous(new ServiceRequest(payload: "Hello Open Telemetry!"));
            var firstAsyncCall = client.ExecuteAsync(new ServiceRequest(payload: "Hello Open Telemetry!"));
            await client.ExecuteAsync(new ServiceRequest(payload: "Hello Open Telemetry!"));
            await firstAsyncCall;
        }
        finally
        {
            client.AbortOrClose();
            tracerProvider?.Shutdown();
            tracerProvider?.Dispose();
            testSource.Dispose();
            WcfInstrumentationActivitySource.Options = null;
        }

        WcfTestHelpers.AssertActivitiesHaveCorrectParentage(stoppedActivities);
    }

    [Fact]
    public async Task ErrorsAreHandledProperlyTest()
    {
        var testSource = new ActivitySource("TestSource");

        List<Activity> stoppedActivities = [];
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var client = new ServiceClient(
            new BasicHttpBinding(BasicHttpSecurityMode.None),
            new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));

        var clientBadUrl = new ServiceClient(
            new BasicHttpBinding(BasicHttpSecurityMode.None),
            new EndpointAddress(new Uri("http://localhost:1/Service")));

        try
        {
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            clientBadUrl.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

            using var parentActivity = testSource.StartActivity("ParentActivity");
            Assert.ThrowsAny<Exception>(client.ErrorSynchronous);
            await Assert.ThrowsAnyAsync<Exception>(client.ErrorAsync);
            Assert.ThrowsAny<Exception>(() => clientBadUrl.ExecuteSynchronous(new ServiceRequest(payload: "Hello Open Telemetry!")));
            await Assert.ThrowsAnyAsync<Exception>(() => clientBadUrl.ExecuteAsync(new ServiceRequest(payload: "Hello Open Telemetry!")));
        }
        finally
        {
            client.AbortOrClose();
            clientBadUrl.AbortOrClose();
            tracerProvider?.Shutdown();
            tracerProvider?.Dispose();
            testSource.Dispose();
            WcfInstrumentationActivitySource.Options = null;
        }

        Assert.Equal(5, stoppedActivities.Count);
        WcfTestHelpers.AssertActivitiesHaveCorrectParentage(stoppedActivities);
    }

    private bool IsListenerShutdownException(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is AggregateException aggregate)
            {
                ex = aggregate.Flatten();
            }

            if (ex is InvalidOperationException && !this.listener.IsListening)
            {
                return true;
            }

            if (ex is HttpListenerException httpEx && (httpEx.ErrorCode is 1 or 6 or 995 or 10057))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ListenAsync()
    {
        this.initialized.TrySetResult(true);

        while (true)
        {
            try
            {
                var context = await this.listener.GetContextAsync().ConfigureAwait(false);

                using var reader = new StreamReader(context.Request.InputStream);

                var request = reader.ReadToEnd();

                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/xml; charset=utf-8";

                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    if (request.Contains("ExecuteWithEmptyActionName"))
                    {
                        writer.Write(@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><ExecuteWithEmptyActionNameResponse xmlns=""http://opentelemetry.io/""><ExecuteResult xmlns:a=""http://schemas.datacontract.org/2004/07/OpenTelemetry.Instrumentation.Wcf.Tests"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Payload>RSP: Hello Open Telemetry!</a:Payload></ExecuteResult></ExecuteWithEmptyActionNameResponse></s:Body></s:Envelope>");
                    }
                    else if (request.Contains("ExecuteSynchronous"))
                    {
                        writer.Write(@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><ExecuteSynchronousResponse xmlns=""http://opentelemetry.io/""><ExecuteResult xmlns:a=""http://schemas.datacontract.org/2004/07/OpenTelemetry.Instrumentation.Wcf.Tests"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Payload>RSP: Hello Open Telemetry!</a:Payload></ExecuteResult></ExecuteSynchronousResponse></s:Body></s:Envelope>");
                    }
                    else
                    {
                        writer.Write(@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><ExecuteResponse xmlns=""http://opentelemetry.io/""><ExecuteResult xmlns:a=""http://schemas.datacontract.org/2004/07/OpenTelemetry.Instrumentation.Wcf.Tests"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Payload>RSP: Hello Open Telemetry!</a:Payload></ExecuteResult></ExecuteResponse></s:Body></s:Envelope>");
                    }
                }

                context.Response.Close();
            }
            catch (Exception ex) when (this.IsListenerShutdownException(ex))
            {
                // Listener was closed before we got into GetContextAsync or
                // Listener was closed while we were in GetContextAsync.
                break;
            }
        }
    }
}
