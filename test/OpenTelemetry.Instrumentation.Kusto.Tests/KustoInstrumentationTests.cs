// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class KustoInstrumentationTests
{
    [Fact]
    public void AddKustoInstrumentation_DoesNotThrow()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        var actual = builder.AddKustoInstrumentation();

        Assert.Same(builder, actual);
    }

    [Fact]
    public void AddKustoInstrumentation_WithOptions_DoesNotThrow()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        var options = new KustoInstrumentationOptions
        {
            EnableTracing = true,
        };

        var actual = builder.AddKustoInstrumentation(options);

        Assert.Same(builder, actual);
    }
}
