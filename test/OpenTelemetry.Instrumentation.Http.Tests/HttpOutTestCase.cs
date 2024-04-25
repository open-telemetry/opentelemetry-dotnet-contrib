// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpOutTestCase
{
    public string Name { get; set; }

    public string Method { get; set; }

    public string Url { get; set; }

    public Dictionary<string, string> Headers { get; set; }

    public int ResponseCode { get; set; }

    public string SpanName { get; set; }

    public bool ResponseExpected { get; set; }

    public bool? RecordException { get; set; }

    public string SpanStatus { get; set; }

    public Dictionary<string, string> SpanAttributes { get; set; }
}
