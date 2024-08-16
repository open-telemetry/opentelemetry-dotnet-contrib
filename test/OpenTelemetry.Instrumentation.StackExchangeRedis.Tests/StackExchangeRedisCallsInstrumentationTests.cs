// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

[Collection("Redis")]
public class StackExchangeRedisCallsInstrumentationTests
{
    /*
        To run the integration tests, set the OTEL_REDISENDPOINT machine-level environment variable to a valid Redis endpoint.

        To use Docker...
         1) Run: docker run -d --name redis -p 6379:6379 redis
         2) Set OTEL_REDISENDPOINT as: localhost:6379
     */

    private const string RedisEndPointEnvVarName = "OTEL_REDISENDPOINT";
    private static readonly string? RedisEndPoint = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(RedisEndPointEnvVarName);

    [Trait("CategoryName", "RedisIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(RedisEndPointEnvVarName)]
    [InlineData("value1")]
    public void SuccessfulCommandTestWithKey(string value)
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(RedisEndPoint!);

        using var connection = ConnectionMultiplexer.Connect(connectionOptions);
        var db = connection.GetDatabase();
        db.KeyDelete("key1");

        var exportedItems = new List<Activity>();
        var sampler = new TestSampler();
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(exportedItems)
                   .SetSampler(sampler)
                   .AddRedisInstrumentation(connection, c => c.SetVerboseDatabaseStatements = true)
                   .Build())
        {
            var prepared = LuaScript.Prepare("redis.call('set', @key, @value)");
            db.ScriptEvaluate(prepared, new { key = (RedisKey)"mykey", value = 123 });

            var redisValue = db.StringGet("key1");

            Assert.False(redisValue.HasValue);

            bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

            Assert.True(set);

            redisValue = db.StringGet("key1");

            Assert.True(redisValue.HasValue);
            Assert.Equal(value, redisValue.ToString());
        }

        // Disposing SDK should flush the Redis profiling session immediately.

        Assert.Equal(4, exportedItems.Count);

        Assert.Equal("EVAL", exportedItems[0].DisplayName);
        Assert.Equal("EVAL redis.call('set', ARGV[1], ARGV[2])", exportedItems[0].GetTagValue(SemanticConventions.AttributeDbStatement));

        VerifyActivityData(exportedItems[1], false, connection.GetEndPoints()[0], true);
        VerifyActivityData(exportedItems[2], true, connection.GetEndPoints()[0], true);
        VerifyActivityData(exportedItems[3], false, connection.GetEndPoints()[0], true);
        VerifySamplingParameters(sampler.LatestSamplingParameters);
    }

    [Trait("CategoryName", "RedisIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(RedisEndPointEnvVarName)]
    [InlineData("value1", null)]
    [InlineData("value1", "serviceKey")]
    public void SuccessfulCommandTest(string value, string? serviceKey)
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(RedisEndPoint!);

        ConnectionMultiplexer? connection = null;
        var exportedItems = new List<Activity>();
        var sampler = new TestSampler();
        using (Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                if (serviceKey is null)
                {
                    services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                    {
                        return connection = ConnectionMultiplexer.Connect(connectionOptions);
                    });
                }
                else
                {
                    services.TryAddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, key) =>
                    {
                        return connection = ConnectionMultiplexer.Connect(connectionOptions);
                    });
                }
            })
            .AddInMemoryExporter(exportedItems)
            .SetSampler(sampler)
            .AddRedisInstrumentation(null, null, serviceKey, c => c.SetVerboseDatabaseStatements = false)
            .Build())
        {
            Assert.NotNull(connection);

            var db = connection.GetDatabase();

            bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

            Assert.True(set);

            var redisValue = db.StringGet("key1");

            Assert.True(redisValue.HasValue);
            Assert.Equal(value, redisValue.ToString());
        }

        // Disposing SDK should flush the Redis profiling session immediately.

        Assert.Equal(2, exportedItems.Count);

        VerifyActivityData(exportedItems[0], true, connection.GetEndPoints()[0], false);
        VerifyActivityData(exportedItems[1], false, connection.GetEndPoints()[0], false);
        VerifySamplingParameters(sampler.LatestSamplingParameters);
    }

    [Fact]
    public async Task ProfilerSessionUsesTheSameDefault()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        var connection = ConnectionMultiplexer.Connect(connectionOptions);

        using var instrumentation = new StackExchangeRedisConnectionInstrumentation(connection, name: null, new StackExchangeRedisInstrumentationOptions());
        var profilerFactory = instrumentation.GetProfilerSessionsFactory();
        var first = profilerFactory();
        var second = profilerFactory();
        ProfilingSession? third = null;
        await Task.Delay(1).ContinueWith((t) => { third = profilerFactory(); });
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Trait("CategoryName", "RedisIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(RedisEndPointEnvVarName)]
    [InlineData("value1")]
    public void CanEnrichActivityFromCommand(string value)
    {
        StackExchangeRedisInstrumentation? instrumentation = null;

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(RedisEndPoint!);
        using var connection = ConnectionMultiplexer.Connect(connectionOptions);

        var exportedItems = new List<Activity>();
        var sampler = new TestSampler();

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .SetSampler(sampler)
            .AddRedisInstrumentation(c => c.Enrich = (activity, command) =>
            {
                if (command.ElapsedTime < TimeSpan.FromMilliseconds(100))
                {
                    activity.AddTag("is_fast", true);
                }
            })
            .ConfigureRedisInstrumentation(i => instrumentation = i);

        using (builder.Build())
        {
            Assert.NotNull(instrumentation);

            instrumentation.AddConnection(connection);

            var db = connection.GetDatabase();

            bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

            Assert.True(set);

            var redisValue = db.StringGet("key1");

            Assert.True(redisValue.HasValue);
            Assert.Equal(value, redisValue.ToString());
        }

        // Disposing SDK should flush the Redis profiling session immediately.

        Assert.Equal(2, exportedItems.Count);

        Assert.Equal(true, exportedItems[0].GetTagValue("is_fast"));
        Assert.Equal(true, exportedItems[1].GetTagValue("is_fast"));
    }

    [Fact]
    public void CheckCacheIsFlushedProperly()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        var connection = ConnectionMultiplexer.Connect(connectionOptions);

        using var instrumentation = new StackExchangeRedisConnectionInstrumentation(connection, name: null, new StackExchangeRedisInstrumentationOptions());
        var profilerFactory = instrumentation.GetProfilerSessionsFactory();

        // start a root level activity
        using Activity rootActivity = new Activity("Parent")
            .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
            .Start();

        Assert.NotNull(rootActivity.Id);

        // get an initial profiler from root activity
        Activity.Current = rootActivity;
        ProfilingSession? profiler0 = profilerFactory();

        // expect different result from synchronous child activity
        using (Activity.Current = new Activity("Child-Span-1").SetParentId(rootActivity.Id).Start())
        {
            var profiler1 = profilerFactory();
            Assert.NotSame(profiler0, profiler1);
        }

        rootActivity.Stop();
        rootActivity.Dispose();

        instrumentation.Flush();
        Assert.Empty(instrumentation.Cache);
    }

    [Fact]
    public async Task ProfilerSessionsHandleMultipleSpans()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        var connection = ConnectionMultiplexer.Connect(connectionOptions);

        using var instrumentation = new StackExchangeRedisConnectionInstrumentation(connection, name: null, new StackExchangeRedisInstrumentationOptions());
        var profilerFactory = instrumentation.GetProfilerSessionsFactory();

        // start a root level activity
        using Activity rootActivity = new Activity("Parent")
            .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
            .Start();

        Assert.NotNull(rootActivity.Id);

        // get an initial profiler from root activity
        Activity.Current = rootActivity;
        ProfilingSession? profiler0 = profilerFactory();

        // expect different result from synchronous child activity
        ProfilingSession? profiler1;
        using (Activity.Current = new Activity("Child-Span-1").SetParentId(rootActivity.Id).Start())
        {
            profiler1 = profilerFactory();
            Assert.NotSame(profiler0, profiler1);
        }

        Activity.Current = rootActivity;

        // expect different result from asynchronous child activity
        using (Activity.Current = new Activity("Child-Span-2").SetParentId(rootActivity.Id).Start())
        {
            // lose async context on purpose
            await Task.Delay(100);

            ProfilingSession? profiler2 = profilerFactory();
            Assert.NotSame(profiler0, profiler2);
            Assert.NotSame(profiler1, profiler2);
        }

        Activity.Current = rootActivity;

        // ensure same result back in root activity
        ProfilingSession? profiles3 = profilerFactory();
        Assert.Same(profiler0, profiles3);
    }

    [Fact]
    public void StackExchangeRedis_DependencyInjection_Success()
    {
        bool connectionMultiplexerPickedFromDI = false;
        bool optionsPickedFromDI = false;

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost");

        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>((sp) =>
        {
            connectionMultiplexerPickedFromDI = true;
            return ConnectionMultiplexer.Connect(connectionOptions);
        });
        services.Configure<StackExchangeRedisInstrumentationOptions>(options =>
        {
            optionsPickedFromDI = true;
        });
        services.AddOpenTelemetry().WithTracing(builder => builder.AddRedisInstrumentation());

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        Assert.True(connectionMultiplexerPickedFromDI);
        Assert.True(optionsPickedFromDI);
    }

    [Fact]
    public void StackExchangeRedis_StackExchangeRedisInstrumentation_Test()
    {
        StackExchangeRedisInstrumentation? instrumentation = null;

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost");

        using var connection = ConnectionMultiplexer.Connect(connectionOptions);
        var sampler = new TestSampler();

        var builder = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddRedisInstrumentation(c => c.Enrich = (activity, command) =>
            {
                if (command.ElapsedTime < TimeSpan.FromMilliseconds(100))
                {
                    activity.AddTag("is_fast", true);
                }
            })
            .ConfigureRedisInstrumentation(i => instrumentation = i);

        using (builder.Build())
        {
            Assert.NotNull(instrumentation);

            var registration1 = instrumentation.AddConnection(connection);

            Assert.NotEmpty(instrumentation.InstrumentedConnections);

            var registration2 = instrumentation.AddConnection(connection);

            Assert.Single(instrumentation.InstrumentedConnections);

            registration1.Dispose();

            Assert.Empty(instrumentation.InstrumentedConnections);

            instrumentation.AddConnection(connection);
        }

        Assert.Empty(instrumentation.InstrumentedConnections);
    }

    private static void VerifyActivityData(Activity activity, bool isSet, EndPoint endPoint, bool setCommandKey = false)
    {
        if (isSet)
        {
            Assert.Equal("SETEX", activity.DisplayName);
            if (setCommandKey)
            {
                Assert.Equal("SETEX key1", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }
            else
            {
                Assert.Equal("SETEX", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }
        }
        else
        {
            Assert.Equal("GET", activity.DisplayName);
            if (setCommandKey)
            {
                Assert.Equal("GET key1", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }
            else
            {
                Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }
        }

        Assert.Equal(Status.Unset, activity.GetStatus());
        Assert.Equal("redis", activity.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.Equal(0, activity.GetTagValue(SemanticConventions.AttributeDbRedisDatabaseIndex));

        if (endPoint is IPEndPoint ipEndPoint)
        {
            Assert.Equal(ipEndPoint.Address.ToString(), activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
            Assert.Equal(ipEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        }
        else if (endPoint is DnsEndPoint dnsEndPoint)
        {
            Assert.Equal(dnsEndPoint.Host, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(dnsEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        }
        else
        {
            Assert.Equal(endPoint.ToString(), activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
    }

    private static void VerifySamplingParameters(SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                   && (string?)kvp.Value == "redis");
    }
}
