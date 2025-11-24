// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// A high-performance string builder that uses ArrayPool and truncates at a maximum length.
/// Once truncation occurs, all future append operations are ignored.
/// </summary>
internal sealed class TruncatingStringBuilder : IDisposable
{
    private const int MaxLength = 255;
    private char[]? buffer;
    private int position;
    private bool isTruncated;

    public TruncatingStringBuilder()
    {
        this.buffer = ArrayPool<char>.Shared.Rent(MaxLength);
        this.position = 0;
        this.isTruncated = false;
    }

    public int Length => this.position;

    public bool IsTruncated => this.isTruncated;

    public void Append(string value)
    {
        if (this.isTruncated || string.IsNullOrEmpty(value))
        {
            return;
        }

        this.Append(value.AsSpan());
    }

    public void Append(ReadOnlySpan<char> value)
    {
        if (this.isTruncated || value.IsEmpty || this.buffer == null)
        {
            return;
        }

        if (this.position + value.Length > MaxLength)
        {
            this.isTruncated = true;
            return;
        }

        value.CopyTo(this.buffer.AsSpan(this.position));
        this.position += value.Length;
    }

    public void Append(char value)
    {
        if (this.isTruncated || this.buffer == null)
        {
            return;
        }

        if (this.position + 1 > MaxLength)
        {
            this.isTruncated = true;
            return;
        }

        this.buffer[this.position++] = value;
    }

    public void TrimEnd()
    {
        if (this.buffer == null || this.position == 0)
        {
            return;
        }

        while (this.position > 0 && char.IsWhiteSpace(this.buffer[this.position - 1]))
        {
            this.position--;
        }
    }

    public override string ToString()
    {
        if (this.buffer == null || this.position == 0)
        {
            return string.Empty;
        }

        return new string(this.buffer, 0, this.position);
    }

    public void Dispose()
    {
        if (this.buffer != null)
        {
            ArrayPool<char>.Shared.Return(this.buffer);
            this.buffer = null;
        }
    }
}
