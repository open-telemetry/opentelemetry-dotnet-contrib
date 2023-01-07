// <copyright file="GenevaLogExporter.cs" company="OpenTelemetry Authors">
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
using System.Runtime.InteropServices;
using OpenTelemetry.Exporter.Geneva.TLDExporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaLogExporter : GenevaBaseExporter<LogRecord>
{
    internal bool IsUsingUnixDomainSocket;

    private bool isDisposed;

    private delegate ExportResult ExportLogRecordFunc(in Batch<LogRecord> batch);

    private readonly ExportLogRecordFunc exportLogRecord;

    private readonly IDisposable exporter;

    public GenevaLogExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        bool useMsgPackExporter;
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

            case TransportProtocol.EtwTld:
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("ETW/TLD cannot be used on non-Windows operating systems.");
                }

                useMsgPackExporter = false;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
        }

        if (useMsgPackExporter)
        {
            var msgPackLogExporter = new MsgPackLogExporter(options);
            this.IsUsingUnixDomainSocket = msgPackLogExporter.IsUsingUnixDomainSocket;
            this.exportLogRecord = (in Batch<LogRecord> batch) => msgPackLogExporter.Export(in batch);
            this.exporter = msgPackLogExporter;
        }
        else
        {
            var tldLogExporter = new TLDLogExporter(options);
            this.IsUsingUnixDomainSocket = false;
            this.exportLogRecord = (in Batch<LogRecord> batch) => tldLogExporter.Export(in batch);
            this.exporter = tldLogExporter;
        }
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        return this.exportLogRecord(in batch);
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
                ExporterEventSource.Log.ExporterException("GenevaLogExporter Dispose failed.", ex);
            }
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }
}
