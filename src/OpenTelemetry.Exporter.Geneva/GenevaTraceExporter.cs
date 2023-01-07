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
using System.Runtime.InteropServices;
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

        var useMsgPackExporter = false;
        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);
        switch (connectionStringBuilder.Protocol)
        {
            case TransportProtocol.Etw:
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("ETW cannot be used on non-Windows operating systems.");
                }

                useMsgPackExporter = true;
                break;

            case TransportProtocol.Unix:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("Unix domain socket should not be used on Windows.");
                }

                useMsgPackExporter = true;
                break;

            /*
            case TransportProtocol.EtwTld:
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("ETW/TLD cannot be used on non-Windows operating systems.");
                }

                var tldTraceExporter = new TLDTraceExporter(options);
                this.IsUsingUnixDomainSocket = false;
                this.exportActivity = (in Batch<Activity> batch) => tldTraceExporter.Export(in batch);
                this.exporter = tldTraceExporter;
                break;
            */
            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
        }

        if (useMsgPackExporter)
        {
            var msgPackExporter = new MsgPackTraceExporter(options);
            this.IsUsingUnixDomainSocket = msgPackExporter.IsUsingUnixDomainSocket;
            this.exportActivity = (in Batch<Activity> batch) => msgPackExporter.Export(in batch);
            this.exporter = msgPackExporter;
        }
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        return this.exportActivity(in batch);
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
