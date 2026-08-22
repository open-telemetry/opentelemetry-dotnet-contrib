// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

public class TracerProviderBuilderExtensionsTests
{
    [Fact]
    public void AddWcfInstrumentation_CalledTwiceWhileFirstProviderIsActive_Throws()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddWcfInstrumentation()
            .Build();

        Assert.Throws<NotSupportedException>(() =>
            Sdk.CreateTracerProviderBuilder()
                .AddWcfInstrumentation()
                .Build());
    }

    [Fact]
    public void AddWcfInstrumentation_CalledAgainAfterProviderDisposed_DoesNotThrow()
    {
        using (Sdk.CreateTracerProviderBuilder()
            .AddWcfInstrumentation()
            .Build())
        {
            // First usage scope
        }

        var exception = Record.Exception(() =>
        {
            // Second usage scope
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddWcfInstrumentation()
                .Build();
        });

        Assert.Null(exception);
    }
}
