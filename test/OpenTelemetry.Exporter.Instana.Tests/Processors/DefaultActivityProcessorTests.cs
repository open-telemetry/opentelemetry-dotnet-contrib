// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors;

public class DefaultActivityProcessorTests
{
    [Fact]
    public void Process_PopulatesInstanaSpan()
    {
        // Arrange
        var activity = new Activity("testOperationName");
        activity.Start();

        Thread.Sleep(200); // Simulate some work being done

        activity.Stop();
        var instanaSpan = new InstanaSpan();

        // Act
        var processor = new DefaultActivityProcessor();
        processor.Process(activity, instanaSpan);

        // Assert
        Assert.False(string.IsNullOrEmpty(instanaSpan.S));
        Assert.False(string.IsNullOrEmpty(instanaSpan.Lt));
        Assert.True(instanaSpan.D > 0);
        Assert.True(instanaSpan.Ts > 0);
        Assert.NotNull(instanaSpan.Data);
        Assert.NotNull(instanaSpan.Data.data);
        Assert.Contains(instanaSpan.Data.data, filter: x => x.Key == "kind" && x.Value.Equals("internal"));
    }
}
