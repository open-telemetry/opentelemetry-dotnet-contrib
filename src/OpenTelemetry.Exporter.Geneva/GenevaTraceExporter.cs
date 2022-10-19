// <copyright file="GenevaTraceExporter.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaTraceExporter : GenevaBaseExporter<Activity>
{
    internal readonly bool IsUsingUnixDomainSocket;

    private bool isDisposed;

    private delegate ExportResult ExportActivityFunc(in Batch<Activity> batch);

    private readonly ExportActivityFunc exportActivity;

    private readonly IDisposable exporter;

    public GenevaTraceExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        var msgPackExporter = new MsgPackTraceExporter(options);
        this.IsUsingUnixDomainSocket = msgPackExporter.IsUsingUnixDomainSocket;
        this.exportActivity = (in Batch<Activity> batch) => msgPackExporter.Export(in batch);
        this.exporter = msgPackExporter;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        return this.exportActivity(batch);
    }

    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                this.exporter.Dispose();
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException("GenevaTraceExporter Dispose failed.", ex);
            }
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }
}
