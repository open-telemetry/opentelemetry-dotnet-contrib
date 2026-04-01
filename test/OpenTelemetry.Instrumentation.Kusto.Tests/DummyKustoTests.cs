// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class DummyKustoTests
{
    [Fact]
    public void TestDummyKusto() => Assert.True(true);
}
