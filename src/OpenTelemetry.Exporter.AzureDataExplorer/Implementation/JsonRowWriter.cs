// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Writes newline-delimited JSON rows into a reusable buffer. Not thread-safe.
/// </summary>
internal sealed class JsonRowWriter : IDisposable
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    private readonly Utf8JsonWriter writer;
    private long rowStart;

    public JsonRowWriter()
    {
        this.Buffer = new MemoryStream();
        this.writer = new Utf8JsonWriter(this.Buffer, WriterOptions);
    }

    public MemoryStream Buffer { get; }

    public int RowCount { get; private set; }

    public void Clear()
    {
        this.writer.Reset(this.Buffer);
        this.Buffer.SetLength(0);
        this.RowCount = 0;
    }

    public Utf8JsonWriter BeginRow()
    {
        this.rowStart = this.Buffer.Length;
        this.writer.Reset(this.Buffer);
        return this.writer;
    }

    public void EndRow()
    {
        this.writer.Flush();
        this.Buffer.WriteByte((byte)'\n');
        this.RowCount++;
    }

    public void AbortRow()
    {
        this.writer.Flush();
        this.Buffer.SetLength(this.rowStart);
        this.Buffer.Position = this.rowStart;
        this.writer.Reset(this.Buffer);
    }

    public void Dispose()
    {
        this.writer.Dispose();
        this.Buffer.Dispose();
    }
}
