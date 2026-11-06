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
    [InlineData(@"evil\", @"evil\\\ ")]
    [InlineData(@"evil\\", @"evil\\\\\ ")]
    [InlineData(@"evil\\\", @"evil\\\\\\\ ")]
    [InlineData(@"evil\ ", @"evil\\\ ")]
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
        Assert.True(HasValidInfluxDBTagDelimiters(line), $"Invalid InfluxDB tag delimiters: {line}");
        var sections = SplitUnescaped(line, ' ');

        // measurement+tags, fields and timestamp.
        Assert.Equal(3, sections.Count);
        Assert.Equal("counter=1i", sections[1]);

        var tags = SplitUnescaped(sections[0], ',');
        Assert.Contains($"zzz={expectedEscapedValue}", tags);
        var expectedValue = tagValue[tagValue.Length - 1] == '\\' ? tagValue + " " : tagValue;
        Assert.Equal(expectedValue, Unescape(expectedEscapedValue));
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

        Assert.Equal(values.Select(value => value + " "), actual);
    }

    [Theory]
    [InlineData(@"zzz\", @"zzz\\\ ")]
    [InlineData(@"zzz\\", @"zzz\\\\\ ")]
    [InlineData(@"zzz\\\", @"zzz\\\\\\\ ")]
    [InlineData(@"zzz\ ", @"zzz\\\ ")]
    [InlineData(@"zzz\=a", @"zzz\\\=a")]
    public void TagKeyBackslashesDoNotCorruptTheRecord(string tagKey, string expectedEscapedKey)
    {
        var body = ExportAndCaptureBody(meter =>
        {
            var counter = meter.CreateCounter<long>("counter");
            counter.Add(1, new KeyValuePair<string, object?>(tagKey, "value"));
        });

        var line = SelectLineContaining(body, "=value");
        Assert.True(HasValidInfluxDBTagDelimiters(line), $"Invalid InfluxDB tag delimiters: {line}");
        var sections = SplitUnescaped(line, ' ');

        Assert.Equal(3, sections.Count);
        Assert.Equal("counter=1i", sections[1]);
        Assert.Contains($"{expectedEscapedKey}=value", SplitUnescaped(sections[0], ','));
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "aaa")]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "zzz")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "aaa")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "zzz")]
    public void TrailingBackslashDoesNotPoisonLaterExports(MetricsSchema schema, string tagKey)
    {
        using var writes = new BlockingCollection<(string Body, HttpStatusCode Status)>();
        using var server = TestHttpServer.RunServer(
            context =>
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = reader.ReadToEnd();
                var valid = body.Split(['\n'], StringSplitOptions.RemoveEmptyEntries).All(HasValidInfluxDBTagDelimiters);
                var status = valid ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest;
                writes.Add((body, status));
                context.Response.StatusCode = (int)status;
                context.Response.Close();
            },
            out var endpoint);

        using var meter = new Meter(new MeterOptions("line-protocol-" + Guid.NewGuid().ToString("N")));
        using var otherMeter = new Meter(new MeterOptions("other-" + Guid.NewGuid().ToString("N")));
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name, otherMeter.Name)
            .AddInfluxDBMetricsExporter(options =>
            {
                options.Endpoint = endpoint;
                options.Bucket = "b";
                options.Org = "o";
                options.Token = "t";
                options.MetricsSchema = schema;
                options.MetricExportIntervalMilliseconds = int.MaxValue;
            })
            .Build();

        var counter = meter.CreateCounter<long>("orders");
        var otherCounter = otherMeter.CreateCounter<long>("other");
        counter.Add(1, new KeyValuePair<string, object?>(tagKey, "kiosk/"));
        provider.ForceFlush();
        Assert.True(writes.TryTake(out var control, TimeSpan.FromSeconds(10)));
        Assert.Equal(HttpStatusCode.NoContent, control.Status);

        counter.Add(1, new KeyValuePair<string, object?>(tagKey, @"kiosk\"));
        var exports = new List<(string Body, HttpStatusCode Status)>();
        for (var i = 1; i <= 3; i++)
        {
            otherCounter.Add(1);
            provider.ForceFlush();
            Assert.True(writes.TryTake(out var write, TimeSpan.FromSeconds(10)));
            exports.Add(write);
            Assert.Contains($@"{tagKey}=kiosk\\", write.Body, StringComparison.Ordinal);
            var otherLine = SelectLineContaining(
                write.Body,
                schema == MetricsSchema.TelegrafPrometheusV1 ? "other," : " other=");
            Assert.Contains(
                schema == MetricsSchema.TelegrafPrometheusV1 ? $"counter={i}i" : $"other={i}i",
                otherLine,
                StringComparison.Ordinal);
        }

        Assert.All(exports, write => Assert.Equal(HttpStatusCode.NoContent, write.Status));
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

    // InfluxDB 1.x/2.x scanTagsKey and scanTagsValue check only the preceding byte,
    // rather than consuming backslashes in pairs. Fields here are numeric and unescaped.
    // https://github.com/influxdata/influxdb/blob/v2.7.12/models/points.go#L655-L713
    private static bool HasValidInfluxDBTagDelimiters(string line)
    {
        var sections = SplitInfluxDBDelimiters(line, ' ');
        return sections.Count == 3
            && SplitInfluxDBDelimiters(sections[0], ',').Skip(1)
                .All(tag => SplitInfluxDBDelimiters(tag, '=').Count == 2);
    }

    private static List<string> SplitInfluxDBDelimiters(string value, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == separator && (i == 0 || value[i - 1] != '\\'))
            {
                parts.Add(value.Substring(start, i - start));
                start = i + 1;
            }
        }

        parts.Add(value.Substring(start));
        return parts;
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
