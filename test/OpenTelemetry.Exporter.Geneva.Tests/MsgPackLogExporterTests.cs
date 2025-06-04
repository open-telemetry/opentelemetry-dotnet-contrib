// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.Sockets;
using System.Runtime.InteropServices;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class MsgPackLogExporterTests
{
    private readonly Socket? server;
    private readonly string path = string.Empty;
    private readonly string connectionString = string.Empty;

    public MsgPackLogExporterTests()
    {
        this.server = null;
        this.path = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            this.connectionString = "EtwSession=OpenTelemetry";
        }
        else
        {
            this.path = GenerateTempFilePath();
            this.connectionString = "Endpoint=unix:" + this.path;
            var endpoint = new UnixDomainSocketEndPoint(this.path);
            this.server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            this.server.Bind(endpoint);
            this.server.Listen(1);
        }
    }

    internal void Dispose()
    {
        this.server?.Dispose();
        try
        {
            File.Delete(this.path);
        }
        catch
        {
        }
    }

    [Fact]
    public void StringSizeLimit_Default_Success()
    {
        var exporterOptions = new GenevaExporterOptions
        {
            ConnectionString = this.connectionString,
        };
        using var exporter = new MsgPackLogExporter(exporterOptions);
    }

    [Fact]
    public void StringSizeLimit_Valid_Success()
    {
        var exporterOptions = new GenevaExporterOptions
        {
            ConnectionString = this.connectionString + ";PrivatePreviewLogMessagePackStringSizeLimit=65360",
        };
        using var exporter = new MsgPackLogExporter(exporterOptions);
    }

    [Fact]
    public void StringSizeLimit_Negative_Success()
    {
        var exporterOptions = new GenevaExporterOptions
        {
            ConnectionString = this.connectionString + ";PrivatePreviewLogMessagePackStringSizeLimit=-1",
        };
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var exporter = new MsgPackLogExporter(exporterOptions);
        });
        Assert.Contains("PrivatePreviewLogMessagePackStringSizeLimit should be greater than zero.", exception.Message);
    }

    [Fact]
    public void StringSizeLimit_ExceedsLimit()
    {
        var exporterOptions = new GenevaExporterOptions
        {
            ConnectionString = this.connectionString + ";PrivatePreviewLogMessagePackStringSizeLimit=65365",
        };
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var exporter = new MsgPackLogExporter(exporterOptions);
        });
        Assert.Contains("The string size limit for MessagePack strings cannot exceed 65360 characters. The provided limit is 65365 characters.", exception.Message);
    }

    private static string GenerateTempFilePath()
    {
        while (true)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }
}
