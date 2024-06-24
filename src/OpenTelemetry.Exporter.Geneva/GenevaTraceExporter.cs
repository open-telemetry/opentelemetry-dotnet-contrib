// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTelemetry.Exporter.Geneva.TldExporter;
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
            var msgPackTraceExporter = new MsgPackTraceExporter(options);
            this.IsUsingUnixDomainSocket = msgPackTraceExporter.IsUsingUnixDomainSocket;
            this.exportActivity = (in Batch<Activity> batch) => msgPackTraceExporter.Export(in batch);
            this.exporter = msgPackTraceExporter;
        }
        else
        {
            var tldTraceExporter = new TldTraceExporter(options);
            this.IsUsingUnixDomainSocket = false;
            this.exportActivity = (in Batch<Activity> batch) => tldTraceExporter.Export(in batch);
            this.exporter = tldTraceExporter;
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
