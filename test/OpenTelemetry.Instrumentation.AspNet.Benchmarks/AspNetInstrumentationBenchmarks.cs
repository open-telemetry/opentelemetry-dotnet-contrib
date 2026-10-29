// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Web;
using System.Web.Routing;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Benchmarks;

/// <summary>
/// Measures the per-request overhead of the ASP.NET instrumentation by driving the
/// <see cref="TelemetryHttpModule"/> callbacks the same way the module does for a request,
/// without the overhead of the rest of the ASP.NET pipeline.
/// </summary>
[MemoryDiagnoser]
public class AspNetInstrumentationBenchmarks
{
    private HttpContext? httpContext;
    private ExecutionContext? executionContext;
    private TracerProvider? tracerProvider;
    private MeterProvider? meterProvider;

    [Flags]
    public enum EnableInstrumentationOption
    {
        /// <summary>
        /// Instrumentation is not enabled for any signal.
        /// </summary>
        None = 0,

        /// <summary>
        /// Instrumentation is enabled only for Traces.
        /// </summary>
        Traces = 1,

        /// <summary>
        /// Instrumentation is enabled only for Metrics.
        /// </summary>
        Metrics = 2,

        /// <summary>
        /// Instrumentation is enabled only for Traces, but no requests are sampled.
        /// </summary>
        UnsampledTraces = 4,
    }

    [Params(EnableInstrumentationOption.None, EnableInstrumentationOption.Traces, EnableInstrumentationOption.Metrics, EnableInstrumentationOption.Traces | EnableInstrumentationOption.Metrics, EnableInstrumentationOption.UnsampledTraces)]
    public EnableInstrumentationOption EnableInstrumentation { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var routeData = new RouteData
        {
            Route = new Route("{controller}/{action}/{id}", new RouteValueDictionary { ["id"] = new { } }, null),
        };

        routeData.Values["controller"] = "Users";
        routeData.Values["action"] = "Get";
        routeData.Values["id"] = "42";

        var request = new HttpRequest(string.Empty, "http://my-service.example.com:8080/Users/Get/42", "name=value")
        {
            RequestContext = new RequestContext
            {
                RouteData = routeData,
            },
        };

        typeof(HttpRequest).GetField("_httpMethod", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(request, "GET");
        typeof(HttpRequest).GetField("_wr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(request, new BenchmarkHttpWorkerRequest());

        this.httpContext = new HttpContext(request, new HttpResponse(new StringWriter()));

        // IIS runs each request on a thread with a default execution context (no ambient Activity or
        // Baggage), so the ambient state set by one request must not be carried over to the next.
        this.executionContext = ExecutionContext.Capture();

        if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces))
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetInstrumentation()
                .Build();
        }
        else if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.UnsampledTraces))
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetInstrumentation()
                .SetSampler(new AlwaysOffSampler())
                .Build();
        }

        if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
        {
            var exportedItems = new List<Metric>();

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.tracerProvider?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void Request()
    {
        // On .NET Framework an ExecutionContext instance can only be run once.
        ExecutionContext.Run(
            this.executionContext!.CreateCopy(),
            static state =>
            {
                var httpContext = (HttpContext)state;
                var options = TelemetryHttpModule.Options;

                // The module wraps the HttpContext separately when the request begins and ends.
                var activity = ActivityHelper.StartAspNetActivity(options.TextMapPropagator, new HttpContextWrapper(httpContext), options.OnRequestStartedCallback);
                ActivityHelper.StopAspNetActivity(options.TextMapPropagator, activity, new HttpContextWrapper(httpContext), options.OnRequestStoppedCallback);
            },
            this.httpContext);
    }

    /// <summary>
    /// Provides the values the instrumentation reads from the worker request (the HTTP version
    /// and the User-Agent header) which a request created without a worker request does not have.
    /// </summary>
    private sealed class BenchmarkHttpWorkerRequest : HttpWorkerRequest
    {
        public override string GetKnownRequestHeader(int index)
            => index == HeaderUserAgent ? "BenchmarkDotNet" : base.GetKnownRequestHeader(index);

        public override string GetHttpVersion() => "HTTP/1.1";

        public override string GetLocalAddress() => "127.0.0.1";

        public override int GetLocalPort() => 8080;

        public override string GetRemoteAddress() => "127.0.0.1";

        public override int GetRemotePort() => 54321;

        public override void EndOfRequest() => throw new NotImplementedException();

        public override void FlushResponse(bool finalFlush) => throw new NotImplementedException();

        public override string GetHttpVerbName() => "GET";

        public override string GetQueryString() => "name=value";

        public override string GetRawUrl() => "/Users/Get/42?name=value";

        public override string GetUriPath() => "/Users/Get/42";

        public override void SendKnownResponseHeader(int index, string value) => throw new NotImplementedException();

        public override void SendResponseFromFile(string filename, long offset, long length) => throw new NotImplementedException();

        public override void SendResponseFromFile(IntPtr handle, long offset, long length) => throw new NotImplementedException();

        public override void SendResponseFromMemory(byte[] data, int length) => throw new NotImplementedException();

        public override void SendStatus(int statusCode, string statusDescription) => throw new NotImplementedException();

        public override void SendUnknownResponseHeader(string name, string value) => throw new NotImplementedException();
    }
}
