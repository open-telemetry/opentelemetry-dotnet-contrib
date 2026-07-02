// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient.Implementation;

internal partial class ElasticsearchRequestPipelineDiagnosticListener : ListenerHandler
{
    internal const string DatabaseSystemName = "elasticsearch";
    internal const string ExceptionCustomPropertyName = "OTel.Elasticsearch.Exception";
    internal const string AttributeDbMethod = "db.method";

    internal static readonly Assembly Assembly = typeof(ElasticsearchRequestPipelineDiagnosticListener).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
#pragma warning disable IDE0370 // Suppression is unnecessary
    internal static readonly string ActivitySourceName = AssemblyName.Name!;
#pragma warning restore IDE0370 // Suppression is unnecessary

    internal static readonly Version SemanticConventionsVersion = new(1, 23, 0);
    internal static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create<ElasticsearchRequestPipelineDiagnosticListener>(SemanticConventionsVersion);

    private const string RequestRegexPattern = @"\n# Request:\r?\n(\{.*)\n# Response";

    private static readonly Version SemanticConventionsVersionNew = new(1, 42, 0);
    private static readonly ActivitySource ActivitySourceNew = ActivitySourceFactory.Create<ElasticsearchRequestPipelineDiagnosticListener>(SemanticConventionsVersionNew);

    private static readonly ActivitySource ActivitySourceBoth = ActivitySourceFactory.Create<ElasticsearchRequestPipelineDiagnosticListener>(null);

#if !NET
    private static readonly Regex ParseRequest = new(RequestRegexPattern, RegexOptions.Compiled | RegexOptions.Singleline);
#endif
    private static readonly ConcurrentDictionary<object, string> MethodNameCache = new();

    // Some Elasticsearch endpoints are namespaced with a fixed, low-cardinality sub-action, e.g. "_cat/aliases" or
    // "_eql/search". Unlike e.g. "_snapshot/{repository}" or "_tasks/{task_id}", where the segment following the
    // namespace is a user-supplied resource identifier, these sub-actions are drawn from a small known vocabulary
    // and are safe to combine into `db.operation.name` (e.g. "cat.aliases") without risking high cardinality.
    private static readonly Dictionary<string, HashSet<string>> NamespacedSubActions = new(StringComparer.Ordinal)
    {
        ["cat"] = new(StringComparer.Ordinal)
        {
            "aliases",
            "allocation",
            "component_templates",
            "count",
            "fielddata",
            "health",
            "indices",
            "master",
            "ml",
            "nodeattrs",
            "nodes",
            "pending_tasks",
            "plugins",
            "recovery",
            "repositories",
            "segments",
            "shards",
            "snapshots",
            "tasks",
            "templates",
            "thread_pool",
            "transforms",
        },
        ["eql"] = new(StringComparer.Ordinal) { "search" },
        ["graph"] = new(StringComparer.Ordinal) { "explore" },
        ["sql"] = new(StringComparer.Ordinal) { "close", "delete_async", "get_async", "get_async_status", "translate" },
    };

    private readonly ElasticsearchClientInstrumentationOptions options;
    private readonly MultiTypePropertyFetcher<Uri> uriFetcher = new("Uri");
    private readonly MultiTypePropertyFetcher<object> methodFetcher = new("Method");
    private readonly MultiTypePropertyFetcher<string> debugInformationFetcher = new("DebugInformation");
    private readonly MultiTypePropertyFetcher<int?> httpStatusFetcher = new("HttpStatusCode");
    private readonly MultiTypePropertyFetcher<Exception> originalExceptionFetcher = new("OriginalException");
    private readonly MultiTypePropertyFetcher<object> failureReasonFetcher = new("FailureReason");
    private readonly MultiTypePropertyFetcher<byte[]> responseBodyFetcher = new("ResponseBodyInBytes");

    public ElasticsearchRequestPipelineDiagnosticListener(ElasticsearchClientInstrumentationOptions options)
        : base("Elasticsearch.Net.RequestPipeline")
    {
        this.options = options;
    }

    public override void OnEventWritten(string name, object? payload)
    {
        var activity = Activity.Current;
        Guard.ThrowIfNull(activity);
        switch (name)
        {
            case "CallElasticsearch.Start":
                this.OnStartActivity(activity, payload);
                break;
            case "CallElasticsearch.Stop":
                this.OnStopActivity(activity, payload);
                break;
            default:
                break;
        }
    }

    private static string GetDisplayName(Activity activity, object? method, string? elasticType, string? dbOperationName, bool useNewSpanNameFormat)
    {
        switch (activity.OperationName)
        {
            case "Ping":
                return "Elasticsearch Ping";

            case "CallElasticsearch" when method != null:
                {
                    if (useNewSpanNameFormat)
                    {
                        // Per the stable database semantic conventions, the span name is
                        // "{db.operation.name} {target}", falling back to just the target or operation name
                        // when the other is not available.
                        return dbOperationName == null
                            ? elasticType ?? method.ToString()!
                            : elasticType == null ? dbOperationName : $"{dbOperationName} {elasticType}";
                    }

                    var methodName = MethodNameCache.GetOrAdd(method, $"Elasticsearch {method}");
                    return elasticType == null ? methodName : $"{methodName} {elasticType}";
                }

            default:
                return "Elasticsearch";
        }
    }

    private static string? GetElasticIndex(Uri uri)
    {
        // first segment is always /
        if (uri.Segments.Length < 2)
        {
            return null;
        }

        // operations starting with _ are not indices (_cat, _search, etc.)
#if NET
        if (uri.Segments[1].StartsWith('_'))
#else
        if (uri.Segments[1].StartsWith("_", StringComparison.Ordinal))
#endif
        {
            return null;
        }

        var elasticType = Uri.UnescapeDataString(uri.Segments[1]);

        // multiple indices used, return null to avoid high cardinality
#if NET
        if (elasticType.Contains(',', StringComparison.Ordinal))
#else
        if (elasticType.Contains(','))
#endif
        {
            return null;
        }

#if NET
        if (elasticType.EndsWith('/'))
#else
        if (elasticType.EndsWith("/", StringComparison.Ordinal))
#endif
        {
            elasticType = elasticType.Substring(0, elasticType.Length - 1);
        }

        return elasticType;
    }

    private static string? GetDbOperationName(Uri uri, object? method)
    {
        // The `_doc`/`_create` endpoints map to different logical operations depending on the HTTP method used.
        var segments = uri.Segments;
        string? endpoint = null;
        string? @namespace = null;

        for (var i = 1; i < segments.Length; i++)
        {
            var segment = Uri.UnescapeDataString(segments[i]).TrimEnd('/');

            if (segment.Length == 0)
            {
                continue;
            }

            if (segment[0] != '_')
            {
                // Only combine with the following segment when it is a known, fixed sub-action for the current
                // namespace (see NamespacedSubActions); otherwise, it is an index/id/other identifier and is
                // ignored to avoid introducing unbounded cardinality into `db.operation.name`.
                if (endpoint != null &&
                    @namespace == null &&
                    NamespacedSubActions.TryGetValue(endpoint, out var subActions) &&
                    subActions.Contains(segment))
                {
                    @namespace = endpoint;
                    endpoint = segment;
                }

                continue;
            }

            endpoint = segment.Substring(1);
            @namespace = null;
        }

        if (endpoint == null)
        {
            return null;
        }

        var methodName = method?.ToString();

        if (string.Equals(endpoint, "doc", StringComparison.Ordinal) ||
            string.Equals(endpoint, "create", StringComparison.Ordinal))
        {
            endpoint = methodName?.ToUpperInvariant() switch
            {
                "GET" => "get",
                "HEAD" => "exists",
                "PUT" => "index",
                "POST" => "create",
                "DELETE" => "delete",
                _ => endpoint,
            };
        }

        return @namespace == null ? endpoint : $"{@namespace}.{endpoint}";
    }

#if NET
    [GeneratedRegex(RequestRegexPattern, RegexOptions.Singleline)]
    private static partial Regex RequestParser();
#else
    private static Regex RequestParser() => ParseRequest;
#endif

    private string ParseAndFormatRequest(string debugInformation)
    {
        if (!this.options.ParseAndFormatRequest)
        {
            return debugInformation;
        }

        var request = RequestParser().Match(debugInformation);
        if (request.Success)
        {
            var body = request.Groups[1]?.Value?.Trim();
            if (body == null)
            {
                return debugInformation;
            }

            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(body));
            if (!JsonDocument.TryParseValue(ref reader, out var doc))
            {
                return debugInformation;
            }

            using (doc)
            using (var stream = new MemoryStream())
            {
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                doc.WriteTo(writer);
                writer.Flush();
                body = Encoding.UTF8.GetString(stream.ToArray());
            }

            return body;
        }

        return debugInformation;
    }

    private void OnStartActivity(Activity activity, object? payload)
    {
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (Sdk.SuppressInstrumentation)
        {
            return;
        }

        Guard.ThrowIfNull(activity);
        if (activity.IsAllDataRequested)
        {
            var uri = this.uriFetcher.Fetch(payload);

            if (uri == null)
            {
                ElasticsearchInstrumentationEventSource.Log.NullPayload(nameof(ElasticsearchRequestPipelineDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            // remove sensitive information like user and password information
            uri = UriHelper.ScrubUserInfo(uri);

            var emitOldAttributes = this.options.EmitOldAttributes;
            var emitNewAttributes = this.options.EmitNewAttributes;

            var activitySource =
                emitOldAttributes && emitNewAttributes ? ActivitySourceBoth :
                emitNewAttributes ? ActivitySourceNew :
                ActivitySource;

            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySource);
            ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);

            var method = this.methodFetcher.Fetch(payload);

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();
            }

            var elasticIndex = GetElasticIndex(uri);
            var dbOperationName = emitNewAttributes ? GetDbOperationName(uri, method) : null;
            activity.DisplayName = GetDisplayName(activity, method, elasticIndex, dbOperationName, emitNewAttributes && !emitOldAttributes);

            if (emitOldAttributes)
            {
                activity.SetTag(SemanticConventions.AttributeDbSystem, DatabaseSystemName);
            }

            if (emitNewAttributes)
            {
                activity.SetTag(SemanticConventions.AttributeDbSystemName, DatabaseSystemName);

                if (dbOperationName != null)
                {
                    activity.SetTag(SemanticConventions.AttributeDbOperationName, dbOperationName);
                }
            }

            if (elasticIndex != null)
            {
                if (emitOldAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeDbName, elasticIndex);
                }

                if (emitNewAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeDbCollectionName, elasticIndex);
                }
            }

            var uriHostNameType = Uri.CheckHostName(uri.Host);
            var isIpAddress = uriHostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6;

            if (emitOldAttributes)
            {
                activity.SetTag(isIpAddress ? SemanticConventions.AttributeNetPeerIp : SemanticConventions.AttributeNetPeerName, uri.Host);
            }

            if (emitNewAttributes)
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, uri.Host);

                if (isIpAddress)
                {
                    activity.SetTag(SemanticConventions.AttributeNetworkPeerAddress, uri.Host);
                }
            }

            if (uri.Port > 0)
            {
                if (emitOldAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerPort, uri.Port);
                }

                if (emitNewAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeServerPort, uri.Port);

                    if (isIpAddress)
                    {
                        activity.SetTag(SemanticConventions.AttributeNetworkPeerPort, uri.Port);
                    }
                }
            }

            if (method != null)
            {
                var methodName = method.ToString();

                if (emitOldAttributes)
                {
                    activity.SetTag(AttributeDbMethod, methodName);
                }

                if (emitNewAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpRequestMethod, methodName);
                }
            }

            activity.SetTag(SemanticConventions.AttributeUrlFull, uri.OriginalString);

            try
            {
                this.options.Enrich?.Invoke(activity, "OnStartActivity", payload);
            }
            catch (Exception ex)
            {
                ElasticsearchInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }
    }

    private void OnStopActivity(Activity activity, object? payload)
    {
        if (activity.IsAllDataRequested)
        {
            var emitOldAttributes = this.options.EmitOldAttributes;
            var emitNewAttributes = this.options.EmitNewAttributes;

            var statusCode = this.httpStatusFetcher.Fetch(payload);
            var activityStatus = SpanHelper.ResolveActivityStatusForHttpStatusCode(activity.Kind, statusCode.GetValueOrDefault());
            activity.SetStatus(activityStatus);

            if (statusCode.HasValue)
            {
                if (emitOldAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, (int)statusCode);
                }

                if (emitNewAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeDbResponseStatusCode, statusCode.Value.ToString(CultureInfo.InvariantCulture));
                }
            }

            var debugInformation = this.debugInformationFetcher.Fetch(payload);
            if (debugInformation != null && this.options.SetDbStatementForRequest)
            {
                var dbStatement = this.ParseAndFormatRequest(debugInformation);
                if (this.options.MaxDbStatementLength > 0 && dbStatement.Length > this.options.MaxDbStatementLength)
                {
                    dbStatement = dbStatement.Substring(0, this.options.MaxDbStatementLength);
                }

                if (emitOldAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, dbStatement);
                }

                if (emitNewAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeDbQueryText, dbStatement);
                }
            }

            var originalException = this.originalExceptionFetcher.Fetch(payload);
            if (originalException != null)
            {
                activity.SetCustomProperty(ExceptionCustomPropertyName, originalException);

                if (emitNewAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeErrorType, originalException.GetType().FullName);
                }

                var failureReason = this.failureReasonFetcher.Fetch(originalException);
                if (failureReason != null)
                {
                    activity.SetStatus(ActivityStatusCode.Error, description: $"{failureReason} {originalException.Message}");
                }

                var responseBody = this.responseBodyFetcher.Fetch(payload);
                if (responseBody != null && responseBody.Length > 0)
                {
                    var response = Encoding.UTF8.GetString(responseBody);
                    activity.SetStatus(ActivityStatusCode.Error, description: $"{failureReason} {originalException.Message}\r\n{response}");
                }

                if (originalException is HttpRequestException)
                {
                    if (originalException.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound })
                    {
                        activity.SetStatus(ActivityStatusCode.Error, description: originalException.Message);
                        return;
                    }

                    if (originalException.InnerException != null)
                    {
                        activity.SetStatus(ActivityStatusCode.Error, description: originalException.Message);
                    }
                }
            }
            else if (emitNewAttributes && activityStatus == ActivityStatusCode.Error && statusCode.HasValue)
            {
                // No exception was thrown, but the response indicates an error (4xx/5xx).
                activity.SetTag(SemanticConventions.AttributeErrorType, statusCode.Value.ToString(CultureInfo.InvariantCulture));
            }

            try
            {
                this.options.Enrich?.Invoke(activity, "OnStopActivity", payload);
            }
            catch (Exception ex)
            {
                ElasticsearchInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }
    }
}
