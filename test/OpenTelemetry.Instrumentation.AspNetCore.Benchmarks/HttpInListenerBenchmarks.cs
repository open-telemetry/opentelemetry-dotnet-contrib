// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Instrumentation.AspNetCore.Benchmarks;

/// <summary>
/// Measures the work done by <see cref="HttpInListener"/> to enrich the <see cref="Activity"/>
/// for a single request, without the overhead of the rest of the ASP.NET Core pipeline.
/// </summary>
[MemoryDiagnoser]
public class HttpInListenerBenchmarks
{
    private readonly HttpInListener listener = new(new AspNetCoreTraceInstrumentationOptions());
    private DefaultHttpContext? context;

    [Params("localhost", "my-service.example.com:8080", "[::1]:5000")]
    public string Host { get; set; } = string.Empty;

    [Params("GET", "FOO")]
    public string Method { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        var context = new DefaultHttpContext();

        context.Request.Method = this.Method;
        context.Request.Scheme = "http";
        context.Request.Path = "/api/users/42";
        context.Request.Protocol = "HTTP/1.1";
        context.Request.Headers.Host = this.Host;
        context.Request.Headers.UserAgent = "BenchmarkDotNet";

        context.SetEndpoint(new Endpoint(
            requestDelegate: null,
            new EndpointMetadataCollection(new RouteDiagnosticsMetadata("api/users/{id}")),
            displayName: "GetUser"));

        this.context = context;
    }

    [Benchmark]
    public Activity StartAndStop()
    {
        var activity = new Activity(HttpInListener.ActivityOperationName).Start();

        this.listener.OnStartActivity(activity, this.context);
        this.listener.OnStopActivity(activity, this.context);

        activity.Stop();

        return activity;
    }

    private sealed class RouteDiagnosticsMetadata(string route) : IRouteDiagnosticsMetadata
    {
        public string Route { get; } = route;
    }
}
