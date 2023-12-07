// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class BasicTests
{
    [Fact]
    public void AddAspNetInstrumentation_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddAspNetInstrumentation());
    }
}
