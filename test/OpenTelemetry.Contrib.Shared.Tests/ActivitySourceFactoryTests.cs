// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Tests;

public class ActivitySourceFactoryTests
{
    [Fact]
    public void Create_ReturnsActivitySourceWithGivenName()
    {
        // Arrange
        var semanticConventionsVersion = new Version(1, 2, 3, 4);

        // Act
        var actual = ActivitySourceFactory.Create<ActivitySourceFactoryTests>(semanticConventionsVersion);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal("OpenTelemetry.Contrib.Shared.Tests", actual.Name);
        Assert.NotNull(actual.Version);
        Assert.NotEmpty(actual.Version);
        Assert.Equal("https://opentelemetry.io/schemas/1.2.3", actual.TelemetrySchemaUrl);
    }
}
