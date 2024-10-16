// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Exporter.Geneva.Tld;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// An exporter for Geneva logs.
/// </summary>
public class GenevaLogExporter : GenevaBaseExporter<LogRecord>
{
    internal bool IsUsingUnixDomainSocket;

    private readonly ExportLogRecordFunc exportLogRecord;
    private readonly IDisposable exporter;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenevaLogExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="GenevaExporterOptions"/>.</param>
    public GenevaLogExporter(GenevaExporterOptions options)
    {
        Guard.ThrowIfNull(options);

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
                throw new NotSupportedException($"Protocol '{connectionStringBuilder.Protocol}' is not supported");
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
            var tldLogExporter = new TldLogExporter(options);
            this.IsUsingUnixDomainSocket = false;
            this.exportLogRecord = (in Batch<LogRecord> batch) => tldLogExporter.Export(in batch);
            this.exporter = tldLogExporter;
        }
    }

    private delegate ExportResult ExportLogRecordFunc(in Batch<LogRecord> batch);

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        return this.exportLogRecord(in batch);
    }

    /// <inheritdoc/>
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
