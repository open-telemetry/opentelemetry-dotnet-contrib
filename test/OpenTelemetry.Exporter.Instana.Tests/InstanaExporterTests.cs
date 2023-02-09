// <copyright file="InstanaExporterTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public class InstanaExporterTests
{
    private readonly Mock<IInstanaExporterHelper> instanaExporterHelperMock = new Mock<IInstanaExporterHelper>(MockBehavior.Strict);
    private readonly Mock<IActivityProcessor> activityProcessorMock = new Mock<IActivityProcessor>(MockBehavior.Strict);
    private readonly Mock<ISpanSender> spanSenderMock = new Mock<ISpanSender>(MockBehavior.Strict);
    private InstanaSpan instanaSpan;
    private InstanaExporter instanaExporter;

    [Fact]
    public void Export()
    {
        this.activityProcessorMock.Setup(x => x.ProcessAsync(It.IsAny<Activity>(), It.IsAny<InstanaSpan>()))
            .Returns(() => Task.CompletedTask);

        this.instanaExporterHelperMock.Setup(x => x.IsWindows()).Returns(false);
        this.instanaExporterHelperMock.Setup(x => x.GetParentProviderResource(It.IsAny<BaseExporter<Activity>>()))
            .Returns(new Resource(new Dictionary<string, object>()
            {
                { "service.name", "serviceName" }, { "service.instance.id", "serviceInstanceId" },
                { "process.pid", "processPid" }, { "host.id", "hostId" },
            }));

        this.spanSenderMock.Setup(x => x.Enqueue(It.Is<InstanaSpan>(y => this.CloneSpan(y))));

        this.instanaExporter = new InstanaExporter(activityProcessor: this.activityProcessorMock.Object);
        this.instanaExporter.InstanaExporterHelper = this.instanaExporterHelperMock.Object;
        this.instanaExporter.SpanSender = this.spanSenderMock.Object;

        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");
        activity.TraceStateString = "TraceStateString";

        Activity[] activities = new Activity[1] { activity };
        Batch<Activity> batch = new Batch<Activity>(activities, activities.Length);
        var result = this.instanaExporter.Export(in batch);

        this.VerifyAllMocks();

        Assert.Equal(ExportResult.Success, result);
        Assert.NotNull(this.instanaSpan);
        Assert.Equal("processPid", this.instanaSpan.F.E);
        Assert.Equal("hostId", this.instanaSpan.F.H);
        Assert.Equal("serviceName", this.instanaSpan.Data.data["service"]);
        Assert.Equal("testOperationName", this.instanaSpan.Data.data["operation"]);
        Assert.Equal("TraceStateString", this.instanaSpan.Data.data["trace_state"]);
    }

    [Fact]
    public void Export_ProcessPidDoesNotExistButServiceIdExists()
    {
        this.activityProcessorMock.Setup(x => x.ProcessAsync(It.IsAny<Activity>(), It.IsAny<InstanaSpan>()))
            .Returns(() => Task.CompletedTask);

        this.instanaExporterHelperMock.Setup(x => x.IsWindows()).Returns(false);
        this.instanaExporterHelperMock.Setup(x => x.GetParentProviderResource(It.IsAny<BaseExporter<Activity>>()))
            .Returns(new Resource(new Dictionary<string, object>()
            {
                { "service.name", "serviceName" }, { "service.instance.id", "serviceInstanceId" },
                { "host.id", "hostId" },
            }));

        this.spanSenderMock.Setup(x => x.Enqueue(It.Is<InstanaSpan>(y => this.CloneSpan(y))));

        this.instanaExporter = new InstanaExporter(activityProcessor: this.activityProcessorMock.Object);
        this.instanaExporter.InstanaExporterHelper = this.instanaExporterHelperMock.Object;
        this.instanaExporter.SpanSender = this.spanSenderMock.Object;

        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");

        Activity[] activities = new Activity[1] { activity };
        Batch<Activity> batch = new Batch<Activity>(activities, activities.Length);
        var result = this.instanaExporter.Export(in batch);

        this.VerifyAllMocks();

        Assert.Equal(ExportResult.Success, result);
        Assert.NotNull(this.instanaSpan);
        Assert.Equal("serviceInstanceId", this.instanaSpan.F.E);
        Assert.Equal("hostId", this.instanaSpan.F.H);
        Assert.Equal("serviceName", this.instanaSpan.Data.data["service"]);
        Assert.Equal("testOperationName", this.instanaSpan.Data.data["operation"]);
    }

    [Fact]
    public void Export_ExporterIsShotDown()
    {
        this.instanaExporter = new InstanaExporter(activityProcessor: this.activityProcessorMock.Object);
        this.instanaExporter.InstanaExporterHelper = this.instanaExporterHelperMock.Object;
        this.instanaExporter.SpanSender = this.spanSenderMock.Object;

        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");

        this.instanaExporter.Shutdown();

        Activity[] activities = new Activity[1] { activity };
        Batch<Activity> batch = new Batch<Activity>(activities, activities.Length);
        var result = this.instanaExporter.Export(in batch);

        this.VerifyAllMocks();

        Assert.Equal(ExportResult.Failure, result);
        Assert.Null(this.instanaSpan);
    }

    private bool CloneSpan(InstanaSpan span)
    {
        this.instanaSpan = span;
        return true;
    }

    private void VerifyAllMocks()
    {
        this.instanaExporterHelperMock.VerifyAll();
        this.activityProcessorMock.VerifyAll();
        this.spanSenderMock.VerifyAll();
    }
}
