// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto.Implementation;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class TraceRecordExtensionsTests
{
    [Theory]

    // Bare constant version.
    [InlineData("EXC_CTOR", true)]

    // Prefix version.
    [InlineData("EXC_CTOR.SemanticException", true)]
    [InlineData("EXC_CTOR.KustoClientStreamReadException", true)]

    [InlineData("EXC_CTORX", false)]
    [InlineData("EXC_CTOR_SemanticException", false)]

    [InlineData("SOMETHING_ELSE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsException(string? sourceId, bool expected)
    {
        var record = new KustoUtils.TraceRecord { SourceId = sourceId };

        Assert.Equal(expected, record.IsException());
    }

    [Theory]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query", true)]
    [InlineData("MonitoredActivityCompleted: KD.RestClient.ExecuteQuery", false)]
    [InlineData("Exception object created: Kusto.Data.Exceptions.SemanticException", false)]
    public void IsRequestStart(string message, bool expected)
    {
        var record = new KustoUtils.TraceRecord { Message = message };

        Assert.Equal(expected, record.IsRequestStart());
    }

    [Theory]
    [InlineData("MonitoredActivityCompleted: KD.RestClient.ExecuteQuery", true)]
    [InlineData("$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query", false)]
    public void IsActivityComplete(string message, bool expected)
    {
        var record = new KustoUtils.TraceRecord { Message = message };

        Assert.Equal(expected, record.IsActivityComplete());
    }
}
