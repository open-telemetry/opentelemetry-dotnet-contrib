// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Instana.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public static class InstanaSpanFactoryTests
{
    [Fact]
    public static void CreateSpan()
    {
        var actual = InstanaSpanFactory.CreateSpan();

        Assert.NotNull(actual);
        Assert.NotNull(actual.TransformInfo);
        Assert.NotNull(actual.Data);
        Assert.Empty(actual.Data.Values);
        Assert.Empty(actual.Data.Tags);
        Assert.Empty(actual.Data.Events);
    }
}
