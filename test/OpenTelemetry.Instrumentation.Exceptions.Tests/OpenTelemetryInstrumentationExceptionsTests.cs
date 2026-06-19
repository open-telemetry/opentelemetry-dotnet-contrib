// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace OpenTelemetry.Instrumentation.Exceptions.Tests;

public class OpenTelemetryInstrumentationExceptionsTests
{
    [Fact]
    public void AssemblyNameMatchesPackageName()
    {
        var assemblyName = Assembly.Load("OpenTelemetry.Instrumentation.Exceptions").GetName().Name;

        Assert.Equal("OpenTelemetry.Instrumentation.Exceptions", assemblyName);
    }
}
