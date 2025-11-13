// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class KustoTraceProviderBuilderTests
{
    [Fact]
    public void AddKustoInstrumentation_DoesNotThrow()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        var actual = builder.AddKustoInstrumentation();

        Assert.Same(builder, actual);
    }

    [Fact]
    public void AddKustoInstrumentation_WithNullBuilder_ThrowsArgumentNullException()
    {
        TracerProviderBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddKustoInstrumentation());
    }

    [Fact]
    public void AddKustoInstrumentation_WithNullOptions_ThrowsArgumentNullException()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        Action<KustoInstrumentationOptions> options = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddKustoInstrumentation(options));
    }

    [Fact]
    public void AddKustoInstrumentation_WithOptions_DoesNotThrow()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        var actual = builder.AddKustoInstrumentation(options => options.RecordQueryText = true);

        Assert.Same(builder, actual);
    }

    [Fact]
    public void KustoInstrumentationOptions_DefaultRecordQueryTextIsFalse()
    {
        var options = new KustoInstrumentationOptions();

        Assert.False(options.RecordQueryText);
    }
}
