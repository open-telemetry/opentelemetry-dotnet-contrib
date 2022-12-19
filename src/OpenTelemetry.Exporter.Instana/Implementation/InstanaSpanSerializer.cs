// <copyright file="InstanaSpanSerializer.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal class InstanaSpanSerializer
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string COMMA = ",";
    private const string OPEN_BRACE = "{";
    private const string CLOSE_BRACE = "}";
    private const string QUOTE = "\"";
    private const string COLON = ":";
    private const string QUOTE_COLON = QUOTE + COLON;
    private const string QUOTE_COLON_QUOTE = QUOTE + COLON + QUOTE;
    private const string QUOTE_COMMA_QUOTE = QUOTE + COMMA + QUOTE;
#pragma warning restore SA1310 // Field names should not contain underscore
    private static readonly long UnixZeroTime = new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;

    internal IEnumerator GetSpanTagsEnumerator(InstanaSpan instanaSpan)
    {
        return instanaSpan.Data.Tags.GetEnumerator();
    }

    internal IEnumerator GetSpanEventsEnumerator(InstanaSpan instanaSpan)
    {
        return instanaSpan.Data.Events.GetEnumerator();
    }

    internal async Task<byte[]> SerializeToByteArrayAsync(InstanaSpan instanaSpan)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);

        await this.SerializeToStreamWriterAsync(instanaSpan, writer);

        await writer.FlushAsync();
        return stream.ToArray();
    }

    internal async Task SerializeToStreamWriterAsync(InstanaSpan instanaSpan, StreamWriter writer)
    {
        await writer.WriteAsync(OPEN_BRACE);
        await this.AppendProperty(instanaSpan.T, "t", writer);
        await writer.WriteAsync(COMMA);
        await this.AppendProperty(instanaSpan.S, "s", writer);
        await writer.WriteAsync(COMMA);

        if (!string.IsNullOrEmpty(instanaSpan.P))
        {
            await this.AppendProperty(instanaSpan.P, "p", writer);
            await writer.WriteAsync(COMMA);
        }

        if (!string.IsNullOrEmpty(instanaSpan.Lt))
        {
            await this.AppendProperty(instanaSpan.Lt, "lt", writer);
            await writer.WriteAsync(COMMA);
        }

        if (instanaSpan.Tp)
        {
            await this.AppendProperty(true.ToString().ToLower(), "tp", writer);
            await writer.WriteAsync(COMMA);
        }

        if (instanaSpan.K != SpanKind.NOT_SET)
        {
            await this.AppendProperty((((int)instanaSpan.K) + 1).ToString(), "k", writer);
            await writer.WriteAsync(COMMA);
        }

        await this.AppendProperty(instanaSpan.N, "n", writer);
        await writer.WriteAsync(COMMA);
        await this.AppendPropertyAsync(DateToUnixMillis(instanaSpan.Ts), "ts", writer);
        await writer.WriteAsync(COMMA);
        await this.AppendPropertyAsync((long)(instanaSpan.D / 10_000), "d", writer);
        await writer.WriteAsync(COMMA);
        await this.AppendObjectAsync(this.SerializeDataAsync, "data", instanaSpan, writer);
        await writer.WriteAsync(COMMA);
        await this.AppendObjectAsync(SerializeFromAsync, "f", instanaSpan, writer);
        await writer.WriteAsync(COMMA);
        await this.AppendPropertyAsync(instanaSpan.Ec, "ec", writer);
        await writer.WriteAsync(CLOSE_BRACE);
    }

    private static async Task SerializeFromAsync(InstanaSpan instanaSpan, StreamWriter writer)
    {
        await writer.WriteAsync("{\"e\":\"");
        await writer.WriteAsync(instanaSpan.F.E);
        await writer.WriteAsync("\"}");
    }

    private static long DateToUnixMillis(long timeStamp)
    {
        return (timeStamp - UnixZeroTime) / 10000;
    }

    private static async Task SerializeTagsAsync(InstanaSpan instanaSpan, StreamWriter writer)
    {
        await SerializeTagsLogicAsync(instanaSpan.Data.Tags, writer);
    }

    private static async Task SerializeTagsLogicAsync(Dictionary<string, string> tags, StreamWriter writer)
    {
        await writer.WriteAsync(OPEN_BRACE);
        using (var enumerator = tags.GetEnumerator())
        {
            byte i = 0;
            try
            {
                while (enumerator.MoveNext())
                {
                    if (i > 0)
                    {
                        await writer.WriteAsync(COMMA);
                    }
                    else
                    {
                        i = 1;
                    }

                    await writer.WriteAsync(QUOTE);
                    await writer.WriteAsync(enumerator.Current.Key);
                    await writer.WriteAsync(QUOTE_COLON_QUOTE);
                    await writer.WriteAsync(enumerator.Current.Value);
                    await writer.WriteAsync(QUOTE);
                }
            }
            catch (InvalidOperationException)
            {
                // if the collection gets modified while serializing, we might get a collision.
                // There is no good way of preventing this and continuing normally except locking
                // which needs investigation
            }
        }

        await writer.WriteAsync(CLOSE_BRACE);
    }

    private async Task AppendProperty(string value, string name, StreamWriter json)
    {
        await json.WriteAsync(QUOTE);
        await json.WriteAsync(name);
        await json.WriteAsync(QUOTE_COLON_QUOTE);
        await json.WriteAsync(value);
        await json.WriteAsync(QUOTE);
    }

    private async Task AppendPropertyAsync(long value, string name, StreamWriter json)
    {
        await json.WriteAsync(QUOTE);
        await json.WriteAsync(name);
        await json.WriteAsync(QUOTE_COLON);
        await json.WriteAsync(value.ToString());
    }

    private async Task AppendObjectAsync(Func<InstanaSpan, StreamWriter, Task> valueFunction, string name, InstanaSpan instanaSpan, StreamWriter json)
    {
        await json.WriteAsync(QUOTE);
        await json.WriteAsync(name);
        await json.WriteAsync(QUOTE_COLON);
        await valueFunction(instanaSpan, json);
    }

    private async Task SerializeDataAsync(InstanaSpan instanaSpan, StreamWriter writer)
    {
        await writer.WriteAsync(OPEN_BRACE);
        using (var enumerator = instanaSpan.Data.data.GetEnumerator())
        {
            byte i = 0;
            try
            {
                while (enumerator.MoveNext())
                {
                    if (i > 0)
                    {
                        await writer.WriteAsync(COMMA);
                    }
                    else
                    {
                        i = 1;
                    }

                    await writer.WriteAsync(QUOTE);
                    await writer.WriteAsync(enumerator.Current.Key);
                    await writer.WriteAsync(QUOTE_COLON_QUOTE);
                    await writer.WriteAsync(enumerator.Current.Value.ToString());
                    await writer.WriteAsync(QUOTE);
                }
            }
            catch (InvalidOperationException)
            {
                // if the collection gets modified while serializing, we might get a collision.
                // There is no good way of preventing this and continuing normally except locking
                // which needs investigation
            }
        }

        if (instanaSpan.Data.Tags?.Count > 0)
        {
            await writer.WriteAsync(COMMA);

            // serialize tags
            await this.AppendObjectAsync(SerializeTagsAsync, InstanaExporterConstants.TAGS_FIELD, instanaSpan, writer);
        }

        if (instanaSpan.Data.Events?.Count > 0)
        {
            await writer.WriteAsync(COMMA);

            // serialize tags
            await this.AppendObjectAsync(this.SerializeEventsAsync, InstanaExporterConstants.EVENTS_FIELD, instanaSpan, writer);
        }

        await writer.WriteAsync(CLOSE_BRACE);
    }

    private async Task SerializeEventsAsync(InstanaSpan instanaSpan, StreamWriter writer)
    {
        using (var enumerator = instanaSpan.Data.Events.GetEnumerator())
        {
            byte i = 0;
            try
            {
                await writer.WriteAsync("[");
                while (enumerator.MoveNext())
                {
                    if (i > 0)
                    {
                        await writer.WriteAsync(COMMA);
                    }
                    else
                    {
                        i = 1;
                    }

                    await writer.WriteAsync(OPEN_BRACE);
                    await writer.WriteAsync(QUOTE);
                    await writer.WriteAsync(InstanaExporterConstants.EVENT_NAME_FIELD);
                    await writer.WriteAsync(QUOTE_COLON_QUOTE);
                    await writer.WriteAsync(enumerator.Current.Name);
                    await writer.WriteAsync(QUOTE_COMMA_QUOTE);
                    await writer.WriteAsync(InstanaExporterConstants.EVENT_TIMESTAMP_FIELD);
                    await writer.WriteAsync(QUOTE_COLON_QUOTE);
                    await writer.WriteAsync(DateToUnixMillis(enumerator.Current.Ts).ToString());
                    await writer.WriteAsync(QUOTE);

                    if (enumerator.Current.Tags?.Count > 0)
                    {
                        await writer.WriteAsync(COMMA);
                        await writer.WriteAsync(QUOTE);
                        await writer.WriteAsync(InstanaExporterConstants.TAGS_FIELD);
                        await writer.WriteAsync(QUOTE);
                        await writer.WriteAsync(COLON);
                        await SerializeTagsLogicAsync(enumerator.Current.Tags, writer);
                    }

                    await writer.WriteAsync(CLOSE_BRACE);
                }

                await writer.WriteAsync("]");
            }
            catch (InvalidOperationException)
            {
                // if the collection gets modified while serializing, we might get a collision.
                // There is no good way of preventing this and continuing normally except locking
                // which needs investigation
            }
        }
    }
}
