// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class TelemetryHelperTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(599)]
    [InlineData(600)]
    [InlineData(999)]
    [InlineData(-1)]
    public void GetBoxedStatusCodeReturnsBoxedInt32(int statusCode)
    {
        var actual = TelemetryHelper.GetBoxedStatusCode((HttpStatusCode)statusCode);

        var value = Assert.IsType<int>(actual);
        Assert.Equal(statusCode, value);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(599)]
    public void GetBoxedStatusCodeReturnsCachedValueForKnownRange(int statusCode)
    {
        var first = TelemetryHelper.GetBoxedStatusCode((HttpStatusCode)statusCode);
        var second = TelemetryHelper.GetBoxedStatusCode((HttpStatusCode)statusCode);

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(599)]
    [InlineData(600)]
    [InlineData(999)]
    [InlineData(-1)]
    public void GetStatusCodeStringReturnsInvariantString(int statusCode)
    {
        var actual = TelemetryHelper.GetStatusCodeString((HttpStatusCode)statusCode);

        Assert.Equal(statusCode.ToString(CultureInfo.InvariantCulture), actual);
    }
}
