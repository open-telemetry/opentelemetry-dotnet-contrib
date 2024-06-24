// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors;

public class ErrorActivityProcessorTests
{
    private ErrorActivityProcessor errorActivityProcessor = new ErrorActivityProcessor();

    [Fact]
    public async Task Process_ErrorStatusCodeIsSet()
    {
        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");
        InstanaSpan instanaSpan = new InstanaSpan();
        await this.errorActivityProcessor.ProcessAsync(activity, instanaSpan);

        Assert.True(instanaSpan.Ec == 1);
        Assert.NotNull(instanaSpan.Data);
        Assert.NotNull(instanaSpan.Data.data);
        Assert.Equal("Error", instanaSpan.Data.data[InstanaExporterConstants.ERROR_FIELD]);
        Assert.Equal("TestErrorDesc", instanaSpan.Data.data[InstanaExporterConstants.ERROR_DETAIL_FIELD]);
    }

    [Fact]
    public async Task Process_ExistsExceptionEvent()
    {
        Activity activity = new Activity("testOperationName");
        InstanaSpan instanaSpan = new InstanaSpan() { TransformInfo = new Implementation.InstanaSpanTransformInfo() { HasExceptionEvent = true } };
        await this.errorActivityProcessor.ProcessAsync(activity, instanaSpan);

        Assert.True(instanaSpan.Ec == 1);
    }

    [Fact]
    public async Task Process_NoError()
    {
        Activity activity = new Activity("testOperationName");
        InstanaSpan instanaSpan = new InstanaSpan() { TransformInfo = new Implementation.InstanaSpanTransformInfo() };
        await this.errorActivityProcessor.ProcessAsync(activity, instanaSpan);

        Assert.True(instanaSpan.Ec == 0);
    }
}
