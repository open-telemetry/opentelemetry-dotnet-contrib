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

using System.Diagnostics;
using OpenTelemetry.Exporter.Instana.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public class InstanaExporterTests
{
    private readonly TestInstanaExporterHelper instanaExporterHelper = new();
    private readonly TestActivityProcessor activityProcessor = new();
    private readonly TestSpanSender spanSender = new();
    private InstanaSpan instanaSpan;
    private InstanaExporter instanaExporter;

    [Fact]
    public void Export()
    {
        this.instanaExporterHelper.Attributes.Clear();
        this.instanaExporterHelper.Attributes.Add("service.name", "serviceName");
        this.instanaExporterHelper.Attributes.Add("service.instance.id", "serviceInstanceId");
        this.instanaExporterHelper.Attributes.Add("process.pid", "processPid");
        this.instanaExporterHelper.Attributes.Add("host.id", "hostId");

        this.spanSender.OnEnqueue = span => this.CloneSpan(span);

        this.instanaExporter = new InstanaExporter(this.activityProcessor)
        {
            InstanaExporterHelper = this.instanaExporterHelper,
            SpanSender = this.spanSender,
        };

        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");
        activity.TraceStateString = "TraceStateString";

        Activity[] activities = new Activity[1] { activity };
        Batch<Activity> batch = new Batch<Activity>(activities, activities.Length);
        var result = this.instanaExporter.Export(in batch);

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
        this.instanaExporterHelper.Attributes.Clear();
        this.instanaExporterHelper.Attributes.Add("service.name", "serviceName");
        this.instanaExporterHelper.Attributes.Add("service.instance.id", "serviceInstanceId");
        this.instanaExporterHelper.Attributes.Add("host.id", "hostId");

        this.spanSender.OnEnqueue = span => this.CloneSpan(span);

        this.instanaExporter = new InstanaExporter(this.activityProcessor)
        {
            InstanaExporterHelper = this.instanaExporterHelper,
            SpanSender = this.spanSender,
        };

        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");

        Activity[] activities = new Activity[1] { activity };
        Batch<Activity> batch = new Batch<Activity>(activities, activities.Length);
        var result = this.instanaExporter.Export(in batch);

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
        this.instanaExporter = new InstanaExporter(this.activityProcessor)
        {
            InstanaExporterHelper = this.instanaExporterHelper,
            SpanSender = this.spanSender,
        };

        Activity activity = new Activity("testOperationName");
        activity.SetStatus(ActivityStatusCode.Error, "TestErrorDesc");

        this.instanaExporter.Shutdown();

        Activity[] activities = new Activity[1] { activity };
        Batch<Activity> batch = new Batch<Activity>(activities, activities.Length);
        var result = this.instanaExporter.Export(in batch);

        Assert.Equal(ExportResult.Failure, result);
        Assert.Null(this.instanaSpan);
    }

    private bool CloneSpan(InstanaSpan span)
    {
        this.instanaSpan = span;
        return true;
    }
}
