// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class KustoMeterProviderBuilderTests
{
    [Fact]
    public void AddKustoInstrumentation_DoesNotThrow()
    {
        var builder = Sdk.CreateMeterProviderBuilder();

        var actual = builder.AddKustoInstrumentation();

        Assert.Same(builder, actual);
    }

    [Fact]
    public void AddKustoInstrumentation_WithNullBuilder_ThrowsArgumentNullException()
    {
        MeterProviderBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddKustoInstrumentation());
    }
}
