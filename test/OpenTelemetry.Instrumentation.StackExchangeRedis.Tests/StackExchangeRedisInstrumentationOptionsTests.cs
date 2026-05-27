// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

[Collection("Redis")]
public class StackExchangeRedisInstrumentationOptionsTests
{
    [Fact]
    public void EnableEarlyCommandDrain_TruthTable()
    {
        Assert.True(new StackExchangeRedisInstrumentationOptions().EnableEarlyCommandDrain);

        Assert.False(new StackExchangeRedisInstrumentationOptions
        {
            Filter = _ => true,
        }.EnableEarlyCommandDrain);

        Assert.False(new StackExchangeRedisInstrumentationOptions
        {
            Enrich = (_, _) => { },
        }.EnableEarlyCommandDrain);

        Assert.False(new StackExchangeRedisInstrumentationOptions
        {
            Filter = _ => true,
            Enrich = (_, _) => { },
        }.EnableEarlyCommandDrain);
    }
}
