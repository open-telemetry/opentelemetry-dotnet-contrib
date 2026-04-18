// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors;

public class ErrorActivityProcessorTests
{
    [Fact]
    public void Process_ErrorStatusCodeIsSet()
    {
        // Arrange
        var activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");
        var instanaSpan = new InstanaSpan();

        // Act
        var processor = new ErrorActivityProcessor();
        processor.Process(activity, instanaSpan);

        // Assert
        Assert.Equal(1, instanaSpan.Ec);
        Assert.NotNull(instanaSpan.Data);
        Assert.NotNull(instanaSpan.Data.data);
        Assert.Equal("Error", instanaSpan.Data.data[InstanaExporterConstants.ERROR_FIELD]);
        Assert.Equal("TestErrorDesc", instanaSpan.Data.data[InstanaExporterConstants.ERROR_DETAIL_FIELD]);
    }

    [Fact]
    public void Process_ExistsExceptionEvent()
    {
        // Arrange
        var activity = new Activity("testOperationName");

        var instanaSpan = new InstanaSpan()
        {
            TransformInfo = new Implementation.InstanaSpanTransformInfo()
            {
                HasExceptionEvent = true,
            },
        };

        // Act
        var processor = new ErrorActivityProcessor();
        processor.Process(activity, instanaSpan);

        // Assert
        Assert.Equal(1, instanaSpan.Ec);
    }

    [Fact]
    public void Process_NoError()
    {
        // Arrange
        var activity = new Activity("testOperationName");
        var instanaSpan = new InstanaSpan() { TransformInfo = new Implementation.InstanaSpanTransformInfo() };

        // Act
        var processor = new ErrorActivityProcessor();
        processor.Process(activity, instanaSpan);

        // Assert
        Assert.Equal(0, instanaSpan.Ec);
    }
}
