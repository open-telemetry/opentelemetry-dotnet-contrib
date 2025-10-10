// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Exporter.Geneva.Tld;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// An exporter for Geneva traces.
/// </summary>
public class GenevaTraceExporter : GenevaBaseExporter<Activity>
{
    internal readonly bool IsUsingUnixDomainSocket;

    private readonly ExportActivityFunc exportActivity;
    private readonly IDisposable exporter;

    private Resource? resource;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenevaTraceExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="GenevaExporterOptions"/>.</param>
    public GenevaTraceExporter(GenevaExporterOptions options)
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
            case TransportProtocol.Tcp:
            case TransportProtocol.Udp:
            case TransportProtocol.Unspecified:
            default:
                throw new NotSupportedException($"Protocol '{connectionStringBuilder.Protocol}' is not supported");
        }

        if (useMsgPackExporter)
        {
            var msgPackTraceExporter = new MsgPackTraceExporter(options);
            this.IsUsingUnixDomainSocket = msgPackTraceExporter.IsUsingUnixDomainSocket;
            this.exportActivity = msgPackTraceExporter.Export;
            this.exporter = msgPackTraceExporter;
        }
        else
        {
            var tldTraceExporter = new TldTraceExporter(options);
            this.IsUsingUnixDomainSocket = false;
            this.exportActivity = tldTraceExporter.Export;
            this.exporter = tldTraceExporter;
        }
    }

    private delegate ExportResult ExportActivityFunc(in Batch<Activity> batch, Resource resource);

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        return this.exportActivity(in batch, this.Resource);
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
                ExporterEventSource.Log.ExporterException("GenevaTraceExporter Dispose failed.", ex);
            }
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }
}
