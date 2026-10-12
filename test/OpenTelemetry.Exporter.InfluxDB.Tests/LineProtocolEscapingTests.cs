// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.InfluxDB.Tests;

public class LineProtocolEscapingTests
{
    [Theory]
    [InlineData(@"evil\", @"evil\\")]
    [InlineData(@"evil\\", @"evil\\\\")]
    [InlineData(@"evil\\\", @"evil\\\\\\")]
    [InlineData(@"a\,b", @"a\\\,b")]
    [InlineData(@"a\ b", @"a\\\ b")]
    [InlineData(@"a\=b", @"a\\\=b")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData(@"C:\Program Files\dotnet", @"C:\\Program\ Files\\dotnet")]
    [InlineData("safe", "safe")]
    public void TagValueBackslashesDoNotCorruptTheRecord(string tagValue, string expectedEscapedValue)
    {
        var body = ExportAndCaptureBody(meter =>
        {
            var counter = meter.CreateCounter<long>("counter");
            counter.Add(1, new KeyValuePair<string, object?>("zzz", tagValue));
        });

        var line = SelectLineContaining(body, "zzz=");
        var sections = SplitUnescaped(line, ' ');

        // measurement+tags, fields and timestamp.
        Assert.Equal(3, sections.Count);
        Assert.Equal("counter=1i", sections[1]);

        var tags = SplitUnescaped(sections[0], ',');
        Assert.Contains($"zzz={expectedEscapedValue}", tags);
        Assert.Equal(tagValue, Unescape(expectedEscapedValue));
    }

    [Fact]
    public void TagValuesWithDifferentBackslashRunsRemainDistinct()
    {
        string[] values = [@"evil\", @"evil\\", @"evil\\\"];

        var body = ExportAndCaptureBody(meter =>
        {
            var counter = meter.CreateCounter<long>("counter");

            foreach (var value in values)
            {
                counter.Add(1, new KeyValuePair<string, object?>("zzz", value));
            }
        });

        var actual = body.Split(['\n'], StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => SplitUnescaped(SplitUnescaped(line, ' ')[0], ','))
            .Where(tag => tag.StartsWith("zzz=", StringComparison.Ordinal))
            .Select(tag => Unescape(tag.Substring("zzz=".Length)))
            .Distinct()
            .OrderBy(value => value.Length)
            .ToArray();

        Assert.Equal(values, actual);
    }

    [Fact]
    public void TagKeyBackslashesDoNotCorruptTheRecord()
    {
        var body = ExportAndCaptureBody(meter =>
        {
            var counter = meter.CreateCounter<long>("counter");
            counter.Add(1, new KeyValuePair<string, object?>(@"zzz\", "value"));
        });

        var line = SelectLineContaining(body, "=value");
        var sections = SplitUnescaped(line, ' ');

        Assert.Equal(3, sections.Count);
        Assert.Equal("counter=1i", sections[1]);
        Assert.Contains(@"zzz\\=value", SplitUnescaped(sections[0], ','));
    }

    [Fact]
    public void DataPointWithoutRepresentableValuesIsReported()
    {
        using var listener = new InMemoryEventListener(InfluxDBEventSource.Log, EventLevel.Warning);

        // One gauge, two series: a NaN point (s=nan) that line protocol cannot represent and a finite point (s=ok).
        var body = ExportAndCaptureBody(meter => meter.CreateObservableGauge(
            "g",
            () => new[]
            {
                new Measurement<double>(double.NaN, new KeyValuePair<string, object?>("s", "nan")),
                new Measurement<double>(5.0, new KeyValuePair<string, object?>("s", "ok")),
            }));

        Assert.Contains("s=ok", body, StringComparison.Ordinal);
        Assert.DoesNotContain("s=nan", body, StringComparison.Ordinal);

        // The provider exports on ForceFlush and again when it is disposed, so the point can be reported more than once.
        var dropped = listener.Events.Where(e => e.EventName == nameof(InfluxDBEventSource.DataPointDropped)).ToList();
        Assert.NotEmpty(dropped);
        Assert.All(dropped, e => Assert.Equal("g", e.Payload?[0]));
    }

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && value[i + 1] is '\\' or ' ' or ',' or '=')
            {
                i++;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static List<string> SplitUnescaped(string value, char separator)
    {
        var parts = new List<string>();
        var start = 0;

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\')
            {
                i++;
                continue;
            }

            if (value[i] == separator)
            {
                parts.Add(value.Substring(start, i - start));
                start = i + 1;
            }
        }

        parts.Add(value.Substring(start));
        return parts;
    }

    private static string ExportAndCaptureBody(Action<Meter> configureInstrument)
    {
        var bodies = new ConcurrentQueue<string>();
        using var server = TestHttpServer.RunServer(
            ctx =>
            {
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                bodies.Enqueue(reader.ReadToEnd());
                ctx.Response.StatusCode = (int)HttpStatusCode.NoContent;
                ctx.Response.Close();
            },
            out var endpoint);

        using (var meter = new Meter("line-protocol-" + Guid.NewGuid().ToString("N"), "1.0"))
        using (var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInfluxDBMetricsExporter(o =>
            {
                o.Bucket = "b";
                o.Org = "o";
                o.Token = "t";
                o.Endpoint = endpoint;
                o.MetricsSchema = MetricsSchema.TelegrafPrometheusV1;
            })
            .Build())
        {
            configureInstrument(meter);
            provider.ForceFlush();
        }

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (bodies.IsEmpty && DateTime.UtcNow < deadline)
        {
            Thread.Yield();
        }

        Assert.False(bodies.IsEmpty, "The exporter did not send any write request.");

        return string.Join("\n", bodies);
    }

    private static string SelectLineContaining(string body, string mustContain)
    {
        foreach (var line in body.Split(['\n'], StringSplitOptions.RemoveEmptyEntries))
        {
#if NET
            if (line.Contains(mustContain, StringComparison.Ordinal))
#else
            if (line.Contains(mustContain))
#endif
            {
                return line;
            }
        }

        return string.Empty;
    }
}
