// <copyright file="InstanaSpanSerializerTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpenTelemetry.Exporter.Instana.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public static class InstanaSpanSerializerTests
{
    [Fact]
    public static async Task SerializeToStreamWriterAsync()
    {
        InstanaSpan instanaOtelSpan = InstanaSpanFactory.CreateSpan();
        instanaOtelSpan.F = new Implementation.From() { E = "12345", H = "localhost" };
        instanaOtelSpan.N = "otel";
        instanaOtelSpan.T = "hexNumberT1234";
        instanaOtelSpan.S = "hexNumberS1234";
        instanaOtelSpan.P = "hexNumberP1234";
        instanaOtelSpan.Ec = 1;
        instanaOtelSpan.D = 123456;
        instanaOtelSpan.Lt = "hexNumberLT1234567890123";
        instanaOtelSpan.Tp = true;
        instanaOtelSpan.Data.Tags = new Dictionary<string, string>();
        instanaOtelSpan.Data.Tags["tag1Key"] = "tag1Vale";
        instanaOtelSpan.Data.Tags["tag2Key"] = "tag2Vale";
        instanaOtelSpan.Data.data = new Dictionary<string, object>();
        instanaOtelSpan.Data.data["data1Key"] = "data1Vale";
        instanaOtelSpan.Data.data["data2Key"] = "data2Vale";
        instanaOtelSpan.Data.Events = new List<Implementation.SpanEvent>()
        {
            new Implementation.SpanEvent()
            {
                Name = "testEvent", Ts = 111111,
                Tags = new Dictionary<string, string>() { { "eventTagKey", "eventTagValue" } },
            },
            new Implementation.SpanEvent()
            {
                Name = "testEvent2", Ts = 222222,
                Tags = new Dictionary<string, string>() { { "eventTag2Key", "eventTag2Value" } },
            },
        };

        InstanaSpanTest span;
        using (MemoryStream sendBuffer = new MemoryStream(new byte[4096000]))
        {
            using (StreamWriter writer = new StreamWriter(sendBuffer))
            {
                await InstanaSpanSerializer.SerializeToStreamWriterAsync(instanaOtelSpan, writer);
                await writer.FlushAsync();
                long length = sendBuffer.Position;
                sendBuffer.Position = 0;
                sendBuffer.SetLength(length);

                JsonSerializer serializer = null;
                serializer = new JsonSerializer();
                serializer.Converters.Add(new JavaScriptDateTimeConverter());
                serializer.NullValueHandling = NullValueHandling.Ignore;

                TextReader textReader = new StreamReader(sendBuffer);
                JsonReader reader = new JsonTextReader(textReader);
                span = serializer.Deserialize<InstanaSpanTest>(reader);
            }
        }

        Assert.NotNull(span);
        Assert.Equal(instanaOtelSpan.S, span.S);
        Assert.Equal(instanaOtelSpan.T, span.T);
        Assert.Equal(instanaOtelSpan.P, span.P);
        Assert.Equal(instanaOtelSpan.N, span.N);
        Assert.Equal(instanaOtelSpan.F.E, span.F.E);
        Assert.Equal(instanaOtelSpan.Ec, span.Ec);
        Assert.Equal(instanaOtelSpan.D / 10_000, span.D);
        Assert.Equal(instanaOtelSpan.Lt, span.Lt);
        Assert.Equal(instanaOtelSpan.Data.Tags["tag1Key"], span.Data.Tags["tag1Key"]);
        Assert.Equal(instanaOtelSpan.Data.Tags["tag2Key"], span.Data.Tags["tag2Key"]);
        Assert.Equal(instanaOtelSpan.Data.data["data1Key"], span.Data.data["data1Key"]);
        Assert.Equal(instanaOtelSpan.Data.data["data2Key"], span.Data.data["data2Key"]);
        Assert.Equal(instanaOtelSpan.Data.Events[0].Name, span.Data.Events[0].Name);
        Assert.Equal(instanaOtelSpan.Data.Events[0].Tags["eventTagKey"], span.Data.Events[0].Tags["eventTagKey"]);
        Assert.Equal(instanaOtelSpan.Data.Events[1].Name, span.Data.Events[1].Name);
        Assert.Equal(instanaOtelSpan.Data.Events[1].Tags["eventTag2Key"], span.Data.Events[1].Tags["eventTag2Key"]);
    }
}
