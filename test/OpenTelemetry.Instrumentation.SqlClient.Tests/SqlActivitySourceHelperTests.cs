// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class SqlActivitySourceHelperTests
{
    [Fact]
    public void ShouldCalculateDuration()
    {
        var epochStart = DateTimeOffset.FromUnixTimeSeconds(0);
        var duration1 = SqlActivitySourceHelper.CalculateDurationFromTimestamp(epochStart.Ticks, epochStart.AddSeconds(10).Ticks);
        Assert.Equal(10.0, duration1);

        var duration2 = SqlActivitySourceHelper.CalculateDurationFromTimestamp(epochStart.Ticks, epochStart.Ticks);
        Assert.Equal(0.0, duration2);

        var duration3 = SqlActivitySourceHelper.CalculateDurationFromTimestamp(epochStart.Ticks, epochStart.AddMilliseconds(10).Ticks);
        Assert.Equal(0.01, duration3);
    }
}
