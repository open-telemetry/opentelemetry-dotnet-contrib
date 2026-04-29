// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.Exporter.Instana.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public static class InstanaSpanSerializerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    [Fact]
    public static void SerializeToStreamWriterAsync()
    {
        var instanaOtelSpan = InstanaSpanFactory.CreateSpan();
        instanaOtelSpan.F = new Implementation.From { E = "12345", H = "localhost" };
        instanaOtelSpan.N = "otel";
        instanaOtelSpan.T = "hexNumberT1234";
        instanaOtelSpan.S = "hexNumberS1234";
        instanaOtelSpan.P = "hexNumberP1234";
        instanaOtelSpan.Ec = 1;
        instanaOtelSpan.D = 123456;
        instanaOtelSpan.Lt = "hexNumberLT1234567890123";
        instanaOtelSpan.Tp = true;
        instanaOtelSpan.Data.Tags = new Dictionary<string, string> { ["tag1Key"] = "tag1Vale", ["tag2Key"] = "tag2Vale" };
        instanaOtelSpan.Data.Values = new Dictionary<string, object> { ["data1Key"] = "data1Vale", ["data2Key"] = "data2Vale" };
        instanaOtelSpan.Data.Events =
        [
            new()
            {
                Name = "testEvent",
                Ts = 111111,
                Tags = new Dictionary<string, string> { { "eventTagKey", "eventTagValue" } },
            },

            new()
            {
                Name = "testEvent2",
                Ts = 222222,
                Tags = new Dictionary<string, string> { { "eventTag2Key", "eventTag2Value" } },
            }

        ];

        InstanaSpanTest? span;
        using (var stream = new MemoryStream())
        {
            using var writer = new Utf8JsonWriter(stream);

            InstanaSpanSerializer.Serialize(instanaOtelSpan, writer);
            writer.Flush();

            stream.Position = 0;

            span = JsonSerializer.Deserialize<InstanaSpanTest>(stream, SerializerOptions);
        }

        Assert.NotNull(span);
        Assert.Equal(instanaOtelSpan.S, span.S);
        Assert.Equal(instanaOtelSpan.T, span.T);
        Assert.Equal(instanaOtelSpan.P, span.P);
        Assert.Equal(instanaOtelSpan.N, span.N);
        Assert.NotNull(span.F);
        Assert.Equal(instanaOtelSpan.F.E, span.F.E);
        Assert.Equal(instanaOtelSpan.Ec, span.Ec);
        Assert.Equal(instanaOtelSpan.D / 10_000, span.D);
        Assert.Equal(instanaOtelSpan.Lt, span.Lt);
        Assert.NotNull(span.Data);
        Assert.NotNull(span.Data.Tags);
        Assert.Equal(instanaOtelSpan.Data.Tags["tag1Key"], span.Data.Tags["tag1Key"]);
        Assert.Equal(instanaOtelSpan.Data.Tags["tag2Key"], span.Data.Tags["tag2Key"]);
        Assert.NotNull(span.Data.Values);
        Assert.Equal(instanaOtelSpan.Data.Values["data1Key"], span.Data.Values["data1Key"].GetString());
        Assert.Equal(instanaOtelSpan.Data.Values["data2Key"], span.Data.Values["data2Key"].GetString());
        Assert.NotNull(span.Data.Events);
        var event0 = span.Data.Events[0];
        Assert.Equal(instanaOtelSpan.Data.Events[0].Name, event0.Name);
        Assert.NotNull(event0.Tags);
        Assert.Equal(instanaOtelSpan.Data.Events[0].Tags["eventTagKey"], event0.Tags["eventTagKey"]);
        var event1 = span.Data.Events[1];
        Assert.Equal(instanaOtelSpan.Data.Events[1].Name, event1.Name);
        Assert.NotNull(event1.Tags);
        Assert.Equal(instanaOtelSpan.Data.Events[1].Tags["eventTag2Key"], event1.Tags["eventTag2Key"]);
    }
}
