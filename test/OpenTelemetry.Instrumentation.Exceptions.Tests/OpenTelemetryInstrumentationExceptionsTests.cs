// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Exceptions.Tests;

public class OpenTelemetryInstrumentationExceptionsTests
{
    [Xunit.Fact]
    public void AssemblyNameMatchesPackageName()
    {
        var assemblyName = System.Reflection.Assembly.Load("OpenTelemetry.Instrumentation.Exceptions").GetName().Name;

        Xunit.Assert.Equal("OpenTelemetry.Instrumentation.Exceptions", assemblyName);
    }
}
