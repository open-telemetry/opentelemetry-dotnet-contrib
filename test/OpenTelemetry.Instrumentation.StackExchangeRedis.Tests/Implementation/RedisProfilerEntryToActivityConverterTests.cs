// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Net;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

#if !NETFRAMEWORK
using System.Net.Sockets;
#endif
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

[Collection("Redis")]
public class RedisProfilerEntryToActivityConverterTests : IDisposable
{
    private readonly ConnectionMultiplexer connection;
    private readonly TracerProvider tracerProvider;

    public RedisProfilerEntryToActivityConverterTests()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        this.connection = ConnectionMultiplexer.Connect(connectionOptions);

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddRedisInstrumentation(this.connection)
            .Build()!;
    }

    public void Dispose()
    {
        this.tracerProvider.Dispose();
        this.connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ProfilerCommandToActivity_UsesCommandAsName()
    {
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.Equal("SET", result.DisplayName);
    }

    [Fact]
    public void ProfilerCommandToActivity_UsesTimestampAsStartTime()
    {
        var now = DateTimeOffset.Now;
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(now.DateTime);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.Equal(now, result.StartTimeUtc);
    }

    [Fact]
    public void ProfilerCommandToActivity_SetsDbTypeAttributeAsRedis()
    {
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.Equal("redis", result.GetTagValue(SemanticConventions.AttributeDbSystem));
    }

    [Fact]
    public void ProfilerCommandToActivity_UsesCommandAsDbStatementAttribute()
    {
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeDbStatement));
        Assert.Equal("SET", result.GetTagValue(SemanticConventions.AttributeDbStatement));
    }

    [Fact]
    public void ProfilerCommandToActivity_UsesFlagsForFlagsAttribute()
    {
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow, CommandFlags.FireAndForget | CommandFlags.NoRedirect);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.GetTagValue(StackExchangeRedisConnectionInstrumentation.RedisFlagsKeyName));

#if NET8_0
        Assert.Equal("FireAndForget, NoRedirect", result.GetTagValue(StackExchangeRedisConnectionInstrumentation.RedisFlagsKeyName));
#else
        Assert.Equal("PreferMaster, FireAndForget, NoRedirect", result.GetTagValue(StackExchangeRedisConnectionInstrumentation.RedisFlagsKeyName));
#endif
    }

    [Fact]
    public void ProfilerCommandToActivity_UsesIpEndPointAsEndPoint()
    {
        long address = 1;
        int port = 2;

        var activity = new Activity("redis-profiler");
        IPEndPoint ipLocalEndPoint = new IPEndPoint(address, port);
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow, ipLocalEndPoint);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerIp));
        Assert.Equal($"{address}.0.0.0", result.GetTagValue(SemanticConventions.AttributeNetPeerIp));
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        Assert.Equal(port, result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
    }

    [Fact]
    public void ProfilerCommandToActivity_UsesDnsEndPointAsEndPoint()
    {
        var dnsEndPoint = new DnsEndPoint("https://opentelemetry.io/", 443);

        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow, dnsEndPoint);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerName));
        Assert.Equal(dnsEndPoint.Host, result.GetTagValue(SemanticConventions.AttributeNetPeerName));
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        Assert.Equal(dnsEndPoint.Port, result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
    }

#if !NETFRAMEWORK
    [Fact]
    public void ProfilerCommandToActivity_UsesOtherEndPointAsEndPoint()
    {
        var unixEndPoint = new UnixDomainSocketEndPoint("https://opentelemetry.io/");
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow, unixEndPoint);

        var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.NotNull(result.GetTagValue(SemanticConventions.AttributePeerService));
        Assert.Equal(unixEndPoint.ToString(), result.GetTagValue(SemanticConventions.AttributePeerService));
    }
#endif
}
