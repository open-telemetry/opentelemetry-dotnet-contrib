// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Instana.Implementation;

#pragma warning disable SA1402 // File may only contain a single type

internal enum SpanKind
{
    ENTRY,
    EXIT,
    INTERMEDIATE,
    NOT_SET,
}

internal sealed class InstanaSpan
{
    public InstanaSpan()
    {
        this.TransformInfo = new();
        this.N = string.Empty;
        this.T = string.Empty;
        this.Lt = string.Empty;
        this.F = new();
        this.P = string.Empty;
        this.S = string.Empty;
        this.K = SpanKind.NOT_SET;
        this.Data = new Data
        {
            Values = new Dictionary<string, object>(8),
            Events = new(8),
            Tags = new Dictionary<string, string>(2),
        };
    }

    public InstanaSpanTransformInfo TransformInfo
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public string N
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public string T
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public string Lt
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public From F
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public string P
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public string S
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public SpanKind K
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public Data Data
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public long Ts
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public long D
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public bool Tp
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public int Ec
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }
}

internal sealed class From
{
    internal From()
    {
        this.E = string.Empty;
        this.H = string.Empty;
    }

    public string E { get; set; }

    public string H { get; set; }

    internal bool IsEmpty() => string.IsNullOrEmpty(this.E) && string.IsNullOrEmpty(this.H);
}

internal sealed class Data
{
    public Data()
    {
        this.Events = new(8);
        this.Values = new(8);
        this.Tags = new(2);
    }

    public Dictionary<string, object> Values
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public Dictionary<string, string> Tags
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public List<SpanEvent> Events
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }
}

internal sealed class SpanEvent
{
    public SpanEvent()
    {
        this.Name = string.Empty;
        this.Tags = [];
    }

    public string Name
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public long Ts { get; set; }

    public Dictionary<string, string> Tags
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }
}
