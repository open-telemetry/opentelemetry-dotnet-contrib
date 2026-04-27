// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

[Collection("Redis")]
[Trait("CategoryName", "RedisIntegrationTests")]
public class StackExchangeRedisCallsInstrumentationTests(RedisXunitFixture fixture) : IClassFixture<RedisXunitFixture>
{
    private readonly string connectionString = fixture.TypedContainer.GetConnectionString();

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("value1")]
    public void SuccessfulCommandTestWithKey(string value)
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(this.connectionString);

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

            var set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

            Assert.True(set);

            redisValue = db.StringGet("key1");

            Assert.True(redisValue.HasValue);
            Assert.Equal(value, redisValue.ToString());
        }

        // Disposing SDK should flush the Redis profiling session immediately.

        Assert.Equal(4, exportedItems.Count);

        Assert.Equal("EVAL", exportedItems[0].DisplayName);
        Assert.Equal("EVAL redis.call('set', ARGV[1], ARGV[2])", exportedItems[0].GetTagValue(SemanticConventions.AttributeDbStatement));

        VerifyOldActivityData(exportedItems[1], false, connection.GetEndPoints()[0], true);
        VerifyOldActivityData(exportedItems[2], true, connection.GetEndPoints()[0], true);
        VerifyOldActivityData(exportedItems[3], false, connection.GetEndPoints()[0], true);
        VerifySamplingParameters(sampler.LatestSamplingParameters);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("value1", null, true, false)]
    [InlineData("value1", null, false, true)]
    [InlineData("value1", null, true, true)]
    [InlineData("value1", "serviceKey", true, false)]
    [InlineData("value1", "serviceKey", false, true)]
    [InlineData("value1", "serviceKey", true, true)]
    public void SuccessfulCommandTest(
        string value,
        string? serviceKey,
        bool emitOldAttributes,
        bool emitNewAttributes)
    {
        using var scope = SemanticConventionScope.Get(
            (emitOldAttributes, emitNewAttributes) switch
            {
                (true, true) => DatabaseSemanticConventionHelper.DatabaseSemanticConvention.Dupe,
                (false, true) => DatabaseSemanticConventionHelper.DatabaseSemanticConvention.New,
                _ => DatabaseSemanticConventionHelper.DatabaseSemanticConvention.Old,
            });

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(this.connectionString);

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
            .AddRedisInstrumentation(null, null, serviceKey, c =>
            {
                c.SetVerboseDatabaseStatements = false;
            })
            .Build())
        {
            Assert.NotNull(connection);

            var db = connection.GetDatabase();

            var set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

            Assert.True(set);

            var redisValue = db.StringGet("key1");

            Assert.True(redisValue.HasValue);
            Assert.Equal(value, redisValue.ToString());
        }

        // Disposing SDK should flush the Redis profiling session immediately.

        Assert.Equal(2, exportedItems.Count);

        var endpoint = connection.GetEndPoints()[0];

        if (emitOldAttributes)
        {
            VerifyOldActivityData(exportedItems[0], true, endpoint, setCommandKey: false);
            VerifyOldActivityData(exportedItems[1], false, endpoint, setCommandKey: false);
            VerifySamplingParameters(sampler.LatestSamplingParameters);
        }

        if (emitNewAttributes)
        {
            VerifyNewActivityData(exportedItems[0], true, endpoint, setCommandKey: false);
            VerifyNewActivityData(exportedItems[1], false, endpoint, setCommandKey: false);

            // TODO VerifySamplingParameters(sampler.LatestSamplingParameters);
        }

        string? expectedSchemaUrl = (emitOldAttributes, emitNewAttributes) switch
        {
            (false, true) => "https://opentelemetry.io/schemas/1.28.0",
            (true, false) => "https://opentelemetry.io/schemas/1.23.0",
            _ => null,
        };

        foreach (var activity in exportedItems)
        {
            Assert.Equal("OpenTelemetry.Instrumentation.StackExchangeRedis", activity.Source.Name);
            Assert.NotNull(activity.Source.Version);
            Assert.NotEmpty(activity.Source.Version);
            Assert.Equal(expectedSchemaUrl, activity.Source.TelemetrySchemaUrl);
        }
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void SuccessfulCommandWithVerboseStatementsEmitsDetailedNewAttributes()
    {
        using var scope = SemanticConventionScope.Get(DatabaseSemanticConventionHelper.DatabaseSemanticConvention.New);

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(this.connectionString);

        ConnectionMultiplexer? connection = null;
        var exportedItems = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                {
                    return connection = ConnectionMultiplexer.Connect(connectionOptions);
                });
            })
            .AddInMemoryExporter(exportedItems)
            .AddRedisInstrumentation(options =>
            {
                options.SetVerboseDatabaseStatements = true;
            })
            .Build())
        {
            Assert.NotNull(connection);

            var db = connection.GetDatabase();

            var set = db.StringSet("key1", "value1", TimeSpan.FromSeconds(60));

            Assert.True(set);

            var redisValue = db.StringGet("key1");

            Assert.True(redisValue.HasValue);
            Assert.Equal("value1", redisValue.ToString());
        }

        Assert.Equal(2, exportedItems.Count);

        var endpoint = connection.GetEndPoints()[0];
        VerifyNewActivityData(exportedItems[0], true, endpoint, setCommandKey: true);
        VerifyNewActivityData(exportedItems[1], false, endpoint, setCommandKey: true);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ProfilerSessionUsesTheSameDefault()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1_000,
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

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("value1")]
    public void CanEnrichActivityFromCommand(string value)
    {
        StackExchangeRedisInstrumentation? instrumentation = null;

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = true,
        };
        connectionOptions.EndPoints.Add(this.connectionString);
        using var connection = ConnectionMultiplexer.Connect(connectionOptions);

        var exportedItems = new List<Activity>();
        var sampler = new TestSampler();

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .SetSampler(sampler)
            .AddRedisInstrumentation(c => c.Enrich = (activity, context) =>
            {
                if (context.ProfiledCommand.ElapsedTime < TimeSpan.FromMilliseconds(100))
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

            var set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

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

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void CheckCacheIsFlushedProperly()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1_000,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        var connection = ConnectionMultiplexer.Connect(connectionOptions);

        using var instrumentation = new StackExchangeRedisConnectionInstrumentation(connection, name: null, new StackExchangeRedisInstrumentationOptions());
        var profilerFactory = instrumentation.GetProfilerSessionsFactory();

        // start a root level activity
        using var rootActivity = new Activity("Parent")
            .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
            .Start();

        Assert.NotNull(rootActivity.Id);

        // get an initial profiler from root activity
        Activity.Current = rootActivity;
        var profiler0 = profilerFactory();

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

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ProfilerSessionsHandleMultipleSpans()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1_000,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        var connection = ConnectionMultiplexer.Connect(connectionOptions);

        using var instrumentation = new StackExchangeRedisConnectionInstrumentation(connection, name: null, new StackExchangeRedisInstrumentationOptions());
        var profilerFactory = instrumentation.GetProfilerSessionsFactory();

        // start a root level activity
        using var rootActivity = new Activity("Parent")
            .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
            .Start();

        Assert.NotNull(rootActivity.Id);

        // get an initial profiler from root activity
        Activity.Current = rootActivity;
        var profiler0 = profilerFactory();

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

            var profiler2 = profilerFactory();
            Assert.NotSame(profiler0, profiler2);
            Assert.NotSame(profiler1, profiler2);
        }

        Activity.Current = rootActivity;

        // ensure same result back in root activity
        var profiles3 = profilerFactory();
        Assert.Same(profiler0, profiles3);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void StackExchangeRedis_DependencyInjection_Success()
    {
        var connectionMultiplexerPickedFromDI = false;
        var optionsPickedFromDI = false;

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1_000,
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

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void StackExchangeRedis_StackExchangeRedisInstrumentation_Test()
    {
        StackExchangeRedisInstrumentation? instrumentation = null;

        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1_000,
        };
        connectionOptions.EndPoints.Add("localhost");

        using var connection = ConnectionMultiplexer.Connect(connectionOptions);
        var sampler = new TestSampler();

        var builder = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddRedisInstrumentation(c => c.Enrich = (activity, context) =>
            {
                if (context.ProfiledCommand.ElapsedTime < TimeSpan.FromMilliseconds(100))
                {
                    activity.AddTag("is_fast", true);
                }
            })
            .ConfigureRedisInstrumentation(i => instrumentation = i);

        using (builder.Build())
        {
            Assert.NotNull(instrumentation);

            var registration = instrumentation.AddConnection(connection);

            Assert.NotEmpty(instrumentation.InstrumentedConnections);

            registration.Dispose();

            Assert.Empty(instrumentation.InstrumentedConnections);

            instrumentation.AddConnection(connection);
        }

        Assert.Empty(instrumentation.InstrumentedConnections);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("value1")]
    public void FilterOption_FiltersOutSpecifiedCommands(string value)
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add(this.connectionString);

        using var connection = ConnectionMultiplexer.Connect(connectionOptions);
        var db = connection.GetDatabase();
        db.KeyDelete("key1");

        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddRedisInstrumentation(connection, options =>
            {
                options.Filter = context => !string.Equals(context.ProfiledCommand.Command, "GET", StringComparison.OrdinalIgnoreCase);
            })
            .Build())
        {
            db.StringSet("key1", value);
            db.StringGet("key1");
        }

        Assert.Single(exportedItems);
        Assert.Equal("SET", exportedItems[0].DisplayName);
    }

    private static void VerifyOldActivityData(Activity activity, bool isSet, EndPoint endPoint, bool setCommandKey = false)
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

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("redis", activity.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.Equal(0, activity.GetTagValue(StackExchangeRedisConnectionInstrumentation.RedisDatabaseIndexKeyName));

        VerifyEndPoint(activity, endPoint);
    }

    private static void VerifyNewActivityData(Activity activity, bool isSet, EndPoint endPoint, bool setCommandKey)
    {
        var displayName = "SETEX";
        var dbOperationName = "SETEX";
        var dbQueryText = setCommandKey ? "SETEX key1" : "SETEX";

        if (!isSet)
        {
            displayName = "GET";
            dbOperationName = "GET";
            dbQueryText = setCommandKey ? "GET key1" : "GET";
        }

        Assert.Equal(displayName, activity.DisplayName);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("redis", activity.GetTagValue(SemanticConventions.AttributeDbSystemName));
        Assert.Equal("0", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        Assert.Equal(dbOperationName, activity.GetTagValue(SemanticConventions.AttributeDbOperationName));
        Assert.Equal(dbQueryText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));

        Assert.Equal("OpenTelemetry.Instrumentation.StackExchangeRedis", activity.Source.Name);
        Assert.NotNull(activity.Source.Version);
        Assert.NotEmpty(activity.Source.Version);

        VerifyEndPoint(activity, endPoint);
    }

    private static void VerifyEndPoint(Activity activity, EndPoint endPoint)
    {
        if (endPoint is IPEndPoint ipEndPoint)
        {
            Assert.Equal(ipEndPoint.Address.ToString(), activity.GetTagValue(SemanticConventions.AttributeServerAddress));
            Assert.Equal(ipEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
            Assert.Equal(ipEndPoint.Address.ToString(), activity.GetTagValue(SemanticConventions.AttributeNetworkPeerAddress));
            Assert.Equal(ipEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeNetworkPeerPort));
        }
        else if (endPoint is DnsEndPoint dnsEndPoint)
        {
            Assert.Equal(dnsEndPoint.Host, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
            Assert.Equal(dnsEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        }
        else
        {
            var tags = activity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.DoesNotContain(SemanticConventions.AttributeServerAddress, tags.Keys);
            Assert.DoesNotContain(SemanticConventions.AttributeServerPort, tags.Keys);
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

    private static class SemanticConventionScope
    {
        public static IDisposable Get(DatabaseSemanticConventionHelper.DatabaseSemanticConvention convention)
        {
            var value = convention switch
            {
                DatabaseSemanticConventionHelper.DatabaseSemanticConvention.Dupe => "database/dup",
                DatabaseSemanticConventionHelper.DatabaseSemanticConvention.New => "database",
                DatabaseSemanticConventionHelper.DatabaseSemanticConvention.Old or _ => string.Empty,
            };

            return EnvironmentVariableScope.Create("OTEL_SEMCONV_STABILITY_OPT_IN", value);
        }
    }
}
