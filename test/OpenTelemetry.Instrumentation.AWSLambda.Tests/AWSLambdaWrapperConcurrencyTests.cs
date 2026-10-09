// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

[Collection("TracerProviderDependent")]
public sealed class AWSLambdaWrapperConcurrencyTests(ITestOutputHelper output) : IDisposable
{
    private readonly ITestOutputHelper output = output;

    private static int ThreadCount => Math.Min(Math.Max(Environment.ProcessorCount, 4), 16);

    public void Dispose()
    {
        Sdk.CreateTracerProviderBuilder()
           .AddAWSLambdaConfigurations();

        AWSLambdaWrapper.ResetColdStart();
    }

    [Fact]
    public void SequentialInvocationsRecordTheirOwnAttributes()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .Build()!;

        var mismatches = new ConcurrentQueue<string>();

        for (var id = 0; id < 5_000; id++)
        {
            Invoke(tracerProvider, id, mismatches);
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public void ConcurrentInvocationsRecordTheirOwnAttributes()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .Build()!;

        var mismatches = new ConcurrentQueue<string>();
        var nextId = 0;
        var duration = Stopwatch.StartNew();

        var failures = RunOnThreads(
            ThreadCount,
            () =>
            {
                while (duration.Elapsed < TimeSpan.FromSeconds(2) && mismatches.IsEmpty)
                {
                    Invoke(tracerProvider, Interlocked.Increment(ref nextId), mismatches);
                }
            });

        this.output.WriteLine($"{Volatile.Read(ref nextId)} invocations on {ThreadCount} threads in {duration.Elapsed}.");

        Assert.Empty(failures);
        Assert.Empty(mismatches);
    }

    [Fact]
    public void ConcurrentFirstInvocationsRecordOneColdStart()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true)
            .Build()!;

        for (var round = 0; round < 50; round++)
        {
            AWSLambdaWrapper.ResetColdStart();

            var coldStarts = 0;
            var invocations = 0;

            using var barrier = new Barrier(ThreadCount);

            var failures = RunOnThreads(
                ThreadCount,
                () =>
                {
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)), "The invocations did not start.");

                    AWSLambdaWrapper.Trace(
                        tracerProvider,
                        (input, context) =>
                        {
                            Interlocked.Increment(ref invocations);

                            if (Activity.Current?.GetTagItem("faas.coldstart") is true)
                            {
                                Interlocked.Increment(ref coldStarts);
                            }
                        },
                        "input",
                        new SampleLambdaContext());
                });

            Assert.Empty(failures);
            Assert.Equal(ThreadCount, invocations);
            Assert.True(coldStarts == 1, $"{coldStarts} of {invocations} concurrent first invocations recorded a cold start in round {round}.");
        }
    }

    private static void Invoke(TracerProvider tracerProvider, int id, ConcurrentQueue<string> mismatches)
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string>
            {
                { "host", $"tenant{id}.example.com" },
                { "x-forwarded-proto", "https" },
            },
            RawPath = $"/tenants/{id}/orders",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                {
                    Method = "GET",
                },
            },
        };

        var context = new SampleLambdaContext { AwsRequestId = $"request-{id}" };

        _ = AWSLambdaWrapper.Trace(
            tracerProvider,
            (input, lambdaContext) =>
            {
                var problem = FindForeignOrMissingAttributes(Activity.Current, id);
                if (problem != null)
                {
                    mismatches.Enqueue($"invocation {id}: {problem}");
                }

                return "ok";
            },
            request,
            context);
    }

    private static string? FindForeignOrMissingAttributes(Activity? activity, int id)
    {
        if (activity == null)
        {
            return "no activity";
        }

        var expected = new Dictionary<string, string>
        {
            ["url.path"] = $"/tenants/{id}/orders",
            ["server.address"] = $"tenant{id}.example.com",
            ["faas.invocation_id"] = $"request-{id}",
        };

        foreach (var attribute in expected)
        {
            var values = activity.TagObjects
                .Where(tag => tag.Key == attribute.Key)
                .Select(tag => tag.Value as string ?? string.Empty)
                .ToList();

            foreach (var value in values)
            {
                if (value != attribute.Value)
                {
                    return $"{attribute.Key}='{value}', expected '{attribute.Value}'";
                }
            }

            if (values.Count != 1)
            {
                return $"{attribute.Key} occurs {values.Count} times, expected once";
            }
        }

        return null;
    }

    private static List<string> RunOnThreads(int threadCount, Action action)
    {
        var failures = new ConcurrentQueue<string>();

        var threads = new Thread[threadCount];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex.ToString());
                }
            })
            {
                IsBackground = true,
            };
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "An invocation did not complete.");
        }

        return [.. failures];
    }
}
