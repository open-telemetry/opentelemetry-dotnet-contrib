// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpOutTestCase
{
    public string Name { get; set; }

    public string Method { get; set; }

#pragma warning disable CA1056
    public string Url { get; set; }
#pragma warning restore CA1056

#pragma warning disable CA2227
    public Dictionary<string, string> Headers { get; set; }
#pragma warning restore CA2227

    public int ResponseCode { get; set; }

    public string SpanName { get; set; }

    public bool ResponseExpected { get; set; }

    public bool? RecordException { get; set; }

    public string SpanStatus { get; set; }

#pragma warning disable CA2227
    public Dictionary<string, string> SpanAttributes { get; set; }
#pragma warning restore CA2227
}
