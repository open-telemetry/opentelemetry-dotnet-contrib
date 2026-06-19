// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests;

public static class WeaverTelemetryVerifier
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerOptions.Default)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static async Task VerifyAsync(
        (IReadOnlyList<Activity> Traces, IReadOnlyList<Metric> Metrics) telemetry,
        Version semanticConventionsVersion,
        WeaverFixture weaver,
        ITestOutputHelper outputHelper,
        IReadOnlyList<KeyValuePair<string, string?>>? suppressAdvice = null,
        CancellationToken cancellationToken = default)
    {
        Assert.NotEqual(0, telemetry.Metrics.Count + telemetry.Traces.Count);
        Assert.NotNull(semanticConventionsVersion);
        Assert.NotNull(weaver);
        Assert.NotNull(outputHelper);

        var json = ToWeaverJson(telemetry.Traces, telemetry.Metrics);

        // Act and Assert
        await VerifyWeaverJsonAsync(
            json,
            semanticConventionsVersion,
            suppressAdvice ?? [],
            weaver,
            outputHelper,
            cancellationToken);
    }

    private static async Task VerifyWeaverJsonAsync(
        string json,
        Version semanticConventionsVersion,
        IReadOnlyList<KeyValuePair<string, string?>>? suppressAdvice,
        WeaverFixture weaver,
        ITestOutputHelper outputHelper,
        CancellationToken cancellationToken)
    {
        // Act
        var actual = await weaver.CheckAsync(json, semanticConventionsVersion, cancellationToken);

        outputHelper.WriteLine($"[weaver] ExitCode: {actual.ExitCode}");
        outputHelper.WriteLine("[weaver] stdout:");
        outputHelper.WriteLine(string.Empty);
        outputHelper.WriteLine(actual.Stdout);

        if (!string.IsNullOrEmpty(actual.Stderr))
        {
            outputHelper.WriteLine(string.Empty);
            outputHelper.WriteLine("[weaver] stderr:");
            outputHelper.WriteLine(string.Empty);
            outputHelper.WriteLine(actual.Stderr);
        }

        // Assert
        Assert.Equal(0, actual.ExitCode);
        Assert.NotEmpty(actual.Stdout);

        var report = JsonSerializer.Deserialize<LiveCheckReport>(actual.Stdout, SerializerOptions);

        AssertReport(
            semanticConventionsVersion,
            suppressAdvice ?? [],
            report,
            outputHelper);
    }

    private static string ToWeaverJson(IReadOnlyList<Activity> activities, IReadOnlyList<Metric> metrics)
    {
        JsonArray root = [];

        if (activities.Count > 0)
        {
            if (GetResourceAttributes(activities[0]) is { Count: > 0 } resourceAttributes)
            {
                root.Add(new JsonObject
                {
                    ["resource"] = new JsonObject
                    {
                        ["attributes"] = resourceAttributes,
                    },
                });
            }

            foreach (var activity in activities)
            {
                var span = new JsonObject
                {
                    ["attributes"] = ToAttributeArray(activity.TagObjects),
                    ["kind"] = ToKind(activity.Kind),
                    ["name"] = activity.DisplayName,
                };

                if (ToSpanEvents(activity.Events) is { Count: > 0 } events)
                {
                    span["span_events"] = events;
                }

                if (ToSpanLinks(activity.Links) is { Count: > 0 } links)
                {
                    span["span_links"] = links;
                }

                if (ToStatus(activity) is { } status)
                {
                    span["status"] = status;
                }

                root.Add(new JsonObject { ["span"] = span });
            }
        }

        foreach (var metric in metrics)
        {
            var item = new JsonObject
            {
                ["name"] = metric.Name,
                ["instrument"] = ToInstrument(metric),
                ["unit"] = metric.Unit ?? string.Empty,
                ["data_points"] = ToMetricDataPoints(metric),
            };

            root.Add(new JsonObject { ["metric"] = item });
        }

        return root.ToJsonString(SerializerOptions);
    }

    private static void AssertReport(
        Version semanticConventionsVersion,
        IReadOnlyList<KeyValuePair<string, string?>> suppressAdvice,
        LiveCheckReport? report,
        ITestOutputHelper outputHelper)
    {
        Assert.NotNull(report);

        var findings = new HashSet<string>();
        var violations = new HashSet<string>();

        foreach (var advice in GetAllAdvice(report).OrderBy((p) => p.Level).ThenBy((p) => p.Id))
        {
            var finding = $"[{advice.Level}] {advice.Id}: {advice.Message}";
            findings.Add(finding);

            if (advice.Level is not ("improvement" or "information"))
            {
                var ignore = false;

                if (advice.Id is { Length: > 0 } id)
                {
                    if (suppressAdvice.Contains(new(id, null)))
                    {
                        ignore = true;
                    }
                    else if (advice.ExtensionData.TryGetValue("signal_name", out var extensionValue) &&
                             extensionValue.ValueKind == JsonValueKind.String &&
                             suppressAdvice.Contains(new(id, extensionValue.GetString())))
                    {
                        ignore = true;
                    }
                }

                if (!ignore)
                {
                    violations.Add(finding);
                }
            }
        }

        foreach (var finding in findings)
        {
            outputHelper.WriteLine($"[weaver]: {finding}");
        }

        if (violations.Count > 0)
        {
            Assert.Fail(
                $"Weaver identified {violations.Count} violation(s) of Semantic Conventions {semanticConventionsVersion} in the telemetry:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations));
        }
    }

    private static IEnumerable<PolicyFinding> GetAllAdvice(LiveCheckReport report)
    {
        foreach (var sample in report.Samples)
        {
            if (sample.Value is { } entity)
            {
                foreach (var advice in GetAdvice(entity))
                {
                    yield return advice;
                }
            }
        }
    }

    private static IEnumerable<PolicyFinding> GetAdvice(LiveCheckEntity entity)
    {
        if (entity.LiveCheckResult?.AllAdvice is { Count: > 0 } entityAdvice)
        {
            foreach (var advice in entityAdvice)
            {
                yield return advice;
            }
        }

        if (entity.Attributes is not null)
        {
            foreach (var attribute in entity.Attributes)
            {
                if (attribute.LiveCheckResult?.AllAdvice is { Count: > 0 } attributeAdvice)
                {
                    foreach (var advice in attributeAdvice)
                    {
                        yield return advice;
                    }
                }
            }
        }

        if (entity.Events is not null)
        {
            foreach (var child in entity.Events)
            {
                foreach (var advice in GetAdvice(child))
                {
                    yield return advice;
                }
            }
        }

        if (entity.Links is not null)
        {
            foreach (var child in entity.Links)
            {
                foreach (var advice in GetAdvice(child))
                {
                    yield return advice;
                }
            }
        }

        if (entity.Resource is not null)
        {
            foreach (var advice in GetAdvice(entity.Resource))
            {
                yield return advice;
            }
        }
    }

    private static JsonArray GetResourceAttributes(Activity activity)
    {
        JsonArray attributes = [];

        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key is not string key || !key.StartsWith("resource.", StringComparison.Ordinal))
            {
                continue;
            }

#if NET
            var name = key["resource.".Length..];
#else
            var name = key.Substring("resource.".Length);
#endif

            attributes.Add(ToWeaverAttribute(name, tag.Value));
        }

        return attributes;
    }

    private static JsonArray ToAttributeArray(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        JsonArray attributes = [];

        foreach (var tag in tags)
        {
            if (!tag.Key.StartsWith("resource.", StringComparison.Ordinal))
            {
                attributes.Add(ToWeaverAttribute(tag.Key, tag.Value));
            }
        }

        return attributes;
    }

    private static JsonArray ToSpanEvents(IEnumerable<ActivityEvent> events)
    {
        JsonArray result = [];

        foreach (var @event in events)
        {
            result.Add(new JsonObject
            {
                ["attributes"] = ToAttributeArray(@event.Tags),
                ["name"] = @event.Name,
            });
        }

        return result;
    }

    private static JsonArray ToSpanLinks(IEnumerable<ActivityLink> links)
    {
        JsonArray result = [];

        foreach (var link in links)
        {
            result.Add(new JsonObject
            {
                ["attributes"] = ToAttributeArray(link.Tags ?? []),
            });
        }

        return result;
    }

    private static JsonObject? ToStatus(Activity activity)
    {
        var code = activity.Status switch
        {
            ActivityStatusCode.Error => "error",
            ActivityStatusCode.Ok => "ok",
            _ => null,
        };

        if (code is null)
        {
            return null;
        }

        var status = new JsonObject
        {
            ["code"] = code,
        };

        if (!string.IsNullOrWhiteSpace(activity.StatusDescription))
        {
            status["message"] = activity.StatusDescription;
        }

        return status;
    }

    private static JsonObject ToWeaverAttribute(string name, object? value)
    {
        var attribute = new JsonObject
        {
            ["name"] = name,
        };

        if (ToJsonValue(value, out var type) is { } jsonValue)
        {
            attribute["value"] = jsonValue;
        }

        if (type is not null)
        {
            attribute["type"] = type;
        }

        return attribute;
    }

    private static string ToKind(ActivityKind kind) => kind switch
    {
        ActivityKind.Client => "client",
        ActivityKind.Consumer => "consumer",
        ActivityKind.Internal => "internal",
        ActivityKind.Producer => "producer",
        ActivityKind.Server => "server",
#pragma warning disable CA1308
        _ => kind.ToString().ToLowerInvariant(),
#pragma warning restore CA1308
    };

    private static JsonNode? ToJsonValue(object? value, out string? type)
    {
        type = null;

        switch (value)
        {
            case null:
                return null;

            case JsonNode node:
                return node;

            case string s:
                return JsonValue.Create(s);

            case bool b:
                return JsonValue.Create(b);

            case int i:
                return JsonValue.Create(i);

            case long l:
                return JsonValue.Create(l);

            case short s16:
                return JsonValue.Create(s16);

            case byte b8:
                return JsonValue.Create(b8);

            case float f:
                return JsonValue.Create(f);

            case double d:
                return JsonValue.Create(d);

            case decimal m:
                return JsonValue.Create(m);

            case string[] strings:
                type = "string[]";
                return ToJsonArray(strings);

            case bool[] bools:
                type = "boolean[]";
                return ToJsonArray(bools);

            case int[] ints:
                type = "long[]";
                return ToJsonArray(ints);

            case long[] longs:
                type = "long[]";
                return ToJsonArray(longs);

            case short[] shorts:
                type = "long[]";
                return ToJsonArray(shorts);

            case byte[] bytes:
                type = "long[]";
                return ToJsonArray(bytes);

            case float[] floats:
                type = "double[]";
                return ToJsonArray(floats);

            case double[] doubles:
                type = "double[]";
                return ToJsonArray(doubles);

            case decimal[] decimals:
                type = "double[]";
                return ToJsonArray(decimals);

            case IEnumerable<string> stringEnumerable:
                type = "string[]";
                return ToJsonArray(stringEnumerable);

            case IEnumerable<bool> boolEnumerable:
                type = "boolean[]";
                return ToJsonArray(boolEnumerable);

            case IEnumerable<int> intEnumerable:
                type = "long[]";
                return ToJsonArray(intEnumerable);

            case IEnumerable<long> longEnumerable:
                type = "long[]";
                return ToJsonArray(longEnumerable);

            case IEnumerable<float> floatEnumerable:
                type = "double[]";
                return ToJsonArray(floatEnumerable);

            case IEnumerable<double> doubleEnumerable:
                type = "double[]";
                return ToJsonArray(doubleEnumerable);

            case IEnumerable<decimal> decimalEnumerable:
                type = "double[]";
                return ToJsonArray(decimalEnumerable);

            default:
                return JsonValue.Create(value.ToString());
        }
    }

    private static JsonArray ToJsonArray<T>(IEnumerable<T> values)
    {
        JsonArray result = [];

        foreach (var value in values)
        {
            result.Add(JsonValue.Create(value));
        }

        return result;
    }

    private static JsonArray ToMetricDataPoints(Metric metric)
    {
        JsonArray dataPoints = [];

        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            var dataPoint = new JsonObject
            {
                ["attributes"] = ToMetricAttributeArray(point.Tags),
            };

            switch (metric.MetricType)
            {
                case MetricType.LongGauge:
                    dataPoint["value"] = point.GetGaugeLastValueLong();
                    break;

                case MetricType.LongSum:
                    dataPoint["value"] = point.GetSumLong();
                    break;

                case MetricType.DoubleGauge:
                    dataPoint["value"] = point.GetGaugeLastValueDouble();
                    break;

                case MetricType.DoubleSum:
                    dataPoint["value"] = point.GetSumDouble();
                    break;

                case MetricType.Histogram:
                    dataPoint["bucket_counts"] = ToBucketCounts(point);
                    dataPoint["count"] = point.GetHistogramCount();
                    dataPoint["sum"] = point.GetHistogramSum();

                    if (point.TryGetHistogramMinMaxValues(out var min, out var max))
                    {
                        dataPoint["min"] = min;
                        dataPoint["max"] = max;
                    }

                    break;

                default:
                    if (TryGetMetricPointValue(point) is { } fallback)
                    {
                        dataPoint["value"] = fallback;
                    }

                    break;
            }

            dataPoints.Add(dataPoint);
        }

        return dataPoints;
    }

    private static JsonArray ToMetricAttributeArray(ReadOnlyTagCollection tags)
    {
        JsonArray attributes = [];

        foreach (var tag in tags)
        {
            attributes.Add(ToWeaverAttribute(tag.Key, tag.Value));
        }

        return attributes;
    }

    private static JsonArray ToBucketCounts(in MetricPoint point)
    {
        JsonArray bucketCounts = [];

        foreach (var bucket in point.GetHistogramBuckets())
        {
            bucketCounts.Add(JsonValue.Create(bucket.BucketCount));
        }

        return bucketCounts;
    }

    private static JsonValue? TryGetMetricPointValue(in MetricPoint point)
    {
        try
        {
            return JsonValue.Create(point.GetSumLong());
        }
        catch
        {
            try
            {
                return JsonValue.Create(point.GetSumDouble());
            }
            catch
            {
                return null;
            }
        }
    }

    private static string ToInstrument(Metric metric) => metric.MetricType switch
    {
        MetricType.DoubleGauge => "gauge",
        MetricType.DoubleSum => "counter",
        MetricType.DoubleSumNonMonotonic => "updowncounter",
        MetricType.ExponentialHistogram => "histogram",
        MetricType.Histogram => "histogram",
        MetricType.LongGauge => "gauge",
        MetricType.LongSum => "counter",
        MetricType.LongSumNonMonotonic => "updowncounter",
        _ => throw new InvalidOperationException($"Unsupported metric type for instrument conversion: {metric.MetricType}."),
    };

    private sealed class LiveCheckReport
    {
        [JsonPropertyName("samples")]
        public List<LiveCheckSample> Samples { get; set; } = [];

        [JsonPropertyName("statistics")]
        public JsonElement Statistics { get; set; }
    }

    private sealed class LiveCheckSample
    {
        [JsonPropertyName("attribute")]
        public LiveCheckEntity? Attribute { get; set; }

        [JsonPropertyName("span")]
        public LiveCheckEntity? Span { get; set; }

        [JsonPropertyName("span_event")]
        public LiveCheckEntity? SpanEvent { get; set; }

        [JsonPropertyName("span_link")]
        public LiveCheckEntity? SpanLink { get; set; }

        [JsonPropertyName("resource")]
        public LiveCheckEntity? Resource { get; set; }

        [JsonPropertyName("metric")]
        public LiveCheckEntity? Metric { get; set; }

        [JsonPropertyName("log")]
        public LiveCheckEntity? Log { get; set; }

        [JsonIgnore]
        public string? Kind =>
            this.Attribute is not null ? "attribute" :
            this.Span is not null ? "span" :
            this.SpanEvent is not null ? "span_event" :
            this.SpanLink is not null ? "span_link" :
            this.Resource is not null ? "resource" :
            this.Metric is not null ? "metric" :
            this.Log is not null ? "log" :
            null;

        [JsonIgnore]
        public LiveCheckEntity? Value =>
            this.Attribute ??
            this.Span ??
            this.SpanEvent ??
            this.SpanLink ??
            this.Resource ??
            this.Metric ??
            this.Log;
    }

    private sealed class LiveCheckEntity
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("event_name")]
        public string? EventName { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("value")]
        public JsonElement? Value { get; set; }

        [JsonPropertyName("attributes")]
        public List<LiveCheckAttribute>? Attributes { get; set; }

        [JsonPropertyName("resource")]
        public LiveCheckEntity? Resource { get; set; }

        [JsonPropertyName("events")]
        public List<LiveCheckEntity>? Events { get; set; }

        [JsonPropertyName("links")]
        public List<LiveCheckEntity>? Links { get; set; }

        [JsonPropertyName("live_check_result")]
        public LiveCheckResult? LiveCheckResult { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }

    private sealed class LiveCheckAttribute
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public JsonElement? Value { get; set; }

        [JsonPropertyName("live_check_result")]
        public LiveCheckResult? LiveCheckResult { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }

    private sealed class LiveCheckResult
    {
        [JsonPropertyName("all_advice")]
        public List<PolicyFinding> AllAdvice { get; set; } = [];

        [JsonPropertyName("highest_advice_level")]
        public string? HighestAdviceLevel { get; set; }
    }

    private sealed class PolicyFinding
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("level")]
        public string? Level { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("advice")]
        public string? Advice { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }
}
