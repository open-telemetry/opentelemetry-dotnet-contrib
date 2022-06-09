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

namespace OpenTelemetry.Exporter.Instana.Tests
{
    public class InstanaSpanSerializerTests
    {
        private InstanaSpanSerializer instanaSpanSerializer = new InstanaSpanSerializer();

        [Fact(Skip = "Borken unit test. https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/405")]
        public async Task SerializeToStreamWriterAsync()
        {
            InstanaSpan instanaSpan = InstanaSpanFactory.CreateSpan();
            instanaSpan.F = new OpenTelemetry.Exporter.Instana.Implementation.From() { E = "12345", H = "localhost" };
            instanaSpan.N = "otel";
            instanaSpan.T = "hexNumberT1234";
            instanaSpan.S = "hexNumberS1234";
            instanaSpan.P = "hexNumberP1234";
            instanaSpan.Ec = 1;
            instanaSpan.D = 123456;
            instanaSpan.Lt = "hexNumberLT1234567890123";
            instanaSpan.Tp = true;
            instanaSpan.Data.Tags = new Dictionary<string, string>();
            instanaSpan.Data.Tags["tag1Key"] = "tag1Vale";
            instanaSpan.Data.Tags["tag2Key"] = "tag2Vale";
            instanaSpan.Data.data = new Dictionary<string, object>();
            instanaSpan.Data.data["data1Key"] = "data1Vale";
            instanaSpan.Data.data["data2Key"] = "data2Vale";
            instanaSpan.Data.Events = new List<OpenTelemetry.Exporter.Instana.Implementation.SpanEvent>()
            {
                new OpenTelemetry.Exporter.Instana.Implementation.SpanEvent()
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
                    await this.instanaSpanSerializer.SerializeToStreamWriterAsync(instanaSpan, writer);
                    await writer.FlushAsync();
                    long length = sendBuffer.Position;
                    sendBuffer.Position = 0;
                    sendBuffer.SetLength(length);

                    Newtonsoft.Json.JsonSerializer serializer = null;
                    serializer = new Newtonsoft.Json.JsonSerializer();
                    serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;

                    TextReader textReader = new StreamReader(sendBuffer);
                    JsonReader reader = new JsonTextReader(textReader);
                    span = serializer.Deserialize<InstanaSpanTest>(reader);
                }
            }

            Assert.NotNull(span);
            Assert.Equal(instanaSpan.S, span.S);
            Assert.Equal(instanaSpan.T, span.T);
            Assert.Equal(instanaSpan.P, span.P);
            Assert.Equal(instanaSpan.N, span.N);
            Assert.Equal(instanaSpan.F.E, span.F.E);
            Assert.Equal(instanaSpan.Ec, span.Ec);
            Assert.Equal(instanaSpan.D, span.D);
            Assert.Equal(instanaSpan.Lt, span.Lt);
            Assert.Equal(instanaSpan.Data.Tags["tag1Key"], span.Data.Tags["tag1Key"]);
            Assert.Equal(instanaSpan.Data.Tags["tag2Key"], span.Data.Tags["tag2Key"]);
            Assert.Equal(instanaSpan.Data.data["data1Key"], span.Data.data["data1Key"]);
            Assert.Equal(instanaSpan.Data.data["data2Key"], span.Data.data["data2Key"]);
            Assert.Equal(instanaSpan.Data.Events[0].Name, span.Data.Events[0].Name);
            Assert.Equal(instanaSpan.Data.Events[0].Tags["eventTagKey"], span.Data.Events[0].Tags["eventTagKey"]);
            Assert.Equal(instanaSpan.Data.Events[1].Name, span.Data.Events[1].Name);
            Assert.Equal(instanaSpan.Data.Events[1].Tags["eventTag2Key"], span.Data.Events[1].Tags["eventTag2Key"]);
        }
    }
}
