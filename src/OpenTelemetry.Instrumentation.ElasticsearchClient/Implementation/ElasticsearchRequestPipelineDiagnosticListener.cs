// <copyright file="ElasticsearchRequestPipelineDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient.Implementation;

internal class ElasticsearchRequestPipelineDiagnosticListener : ListenerHandler
{
    internal const string DatabaseSystemName = "elasticsearch";
    internal const string ExceptionCustomPropertyName = "OTel.Elasticsearch.Exception";
    internal const string AttributeDbMethod = "db.method";

    internal static readonly AssemblyName AssemblyName = typeof(ElasticsearchRequestPipelineDiagnosticListener).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

    private static readonly Regex ParseRequest = new Regex(@"\n# Request:\r?\n(\{.*)\n# Response", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly ConcurrentDictionary<object, string> MethodNameCache = new ConcurrentDictionary<object, string>();

    private readonly ElasticsearchClientInstrumentationOptions options;
    private readonly MultiTypePropertyFetcher<Uri> uriFetcher = new MultiTypePropertyFetcher<Uri>("Uri");
    private readonly MultiTypePropertyFetcher<object> methodFetcher = new MultiTypePropertyFetcher<object>("Method");
    private readonly MultiTypePropertyFetcher<string> debugInformationFetcher = new MultiTypePropertyFetcher<string>("DebugInformation");
    private readonly MultiTypePropertyFetcher<int?> httpStatusFetcher = new MultiTypePropertyFetcher<int?>("HttpStatusCode");
    private readonly MultiTypePropertyFetcher<Exception> originalExceptionFetcher = new MultiTypePropertyFetcher<Exception>("OriginalException");
    private readonly MultiTypePropertyFetcher<object> failureReasonFetcher = new MultiTypePropertyFetcher<object>("FailureReason");
    private readonly MultiTypePropertyFetcher<byte[]> responseBodyFetcher = new MultiTypePropertyFetcher<byte[]>("ResponseBodyInBytes");

    public ElasticsearchRequestPipelineDiagnosticListener(ElasticsearchClientInstrumentationOptions options)
        : base("Elasticsearch.Net.RequestPipeline")
    {
        this.options = options;
    }

    public override void OnStartActivity(Activity activity, object payload)
    {
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (Sdk.SuppressInstrumentation)
        {
            return;
        }

        if (activity.IsAllDataRequested)
        {
            var uri = this.uriFetcher.Fetch(payload);

            if (uri == null)
            {
                ElasticsearchInstrumentationEventSource.Log.NullPayload(nameof(ElasticsearchRequestPipelineDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
            ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);

            var method = this.methodFetcher.Fetch(payload);

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();
            }

            var elasticIndex = GetElasticIndex(uri);
            activity.DisplayName = GetDisplayName(activity, method, elasticIndex);
            activity.SetTag(SemanticConventions.AttributeDbSystem, DatabaseSystemName);

            if (elasticIndex != null)
            {
                activity.SetTag(SemanticConventions.AttributeDbName, elasticIndex);
            }

            var uriHostNameType = Uri.CheckHostName(uri.Host);
            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                activity.SetTag(SemanticConventions.AttributeNetPeerIp, uri.Host);
            }
            else
            {
                activity.SetTag(SemanticConventions.AttributeNetPeerName, uri.Host);
            }

            if (uri.Port > 0)
            {
                activity.SetTag(SemanticConventions.AttributeNetPeerPort, uri.Port);
            }

            if (method != null)
            {
                activity.SetTag(AttributeDbMethod, method.ToString());
            }

            activity.SetTag(SemanticConventions.AttributeDbUrl, uri.OriginalString);

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

    public override void OnStopActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            var statusCode = this.httpStatusFetcher.Fetch(payload);
            activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(statusCode.GetValueOrDefault()));

            if (statusCode.HasValue)
            {
                activity.SetTag(SemanticConventions.AttributeHttpStatusCode, (int)statusCode);
            }

            var debugInformation = this.debugInformationFetcher.Fetch(payload);
            if (debugInformation != null && this.options.SetDbStatementForRequest)
            {
                var dbStatement = this.ParseAndFormatRequest(debugInformation);
                if (this.options.MaxDbStatementLength > 0 && dbStatement.Length > this.options.MaxDbStatementLength)
                {
                    dbStatement = dbStatement.Substring(0, this.options.MaxDbStatementLength);
                }

                activity.SetTag(SemanticConventions.AttributeDbStatement, dbStatement);
            }

            var originalException = this.originalExceptionFetcher.Fetch(payload);
            if (originalException != null)
            {
                activity.SetCustomProperty(ExceptionCustomPropertyName, originalException);

                var failureReason = this.failureReasonFetcher.Fetch(originalException);
                if (failureReason != null)
                {
                    activity.SetStatus(Status.Error.WithDescription($"{failureReason} {originalException.Message}"));
                }

                var responseBody = this.responseBodyFetcher.Fetch(payload);
                if (responseBody != null && responseBody.Length > 0)
                {
                    var response = Encoding.UTF8.GetString(responseBody);
                    activity.SetStatus(Status.Error.WithDescription($"{failureReason} {originalException.Message}\r\n{response}"));
                }

                if (originalException is HttpRequestException)
                {
                    if (originalException.InnerException is SocketException exception)
                    {
                        switch (exception.SocketErrorCode)
                        {
                            case SocketError.HostNotFound:
                                activity.SetStatus(Status.Error.WithDescription(originalException.Message));
                                return;
                        }
                    }

                    if (originalException.InnerException != null)
                    {
                        activity.SetStatus(Status.Error.WithDescription(originalException.Message));
                    }
                }
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

    private static string GetDisplayName(Activity activity, object method, string elasticType = null)
    {
        switch (activity.OperationName)
        {
            case "Ping":
                return "Elasticsearch Ping";
            case "CallElasticsearch" when method != null:
                {
                    var methodName = MethodNameCache.GetOrAdd(method, $"Elasticsearch {method}");
                    if (elasticType == null)
                    {
                        return methodName;
                    }

                    return $"{methodName} {elasticType}";
                }

            default:
                return "Elasticsearch";
        }
    }

    private static string GetElasticIndex(Uri uri)
    {
        // first segment is always /
        if (uri.Segments.Length < 2)
        {
            return null;
        }

        // operations starting with _ are not indices (_cat, _search, etc)
        if (uri.Segments[1].StartsWith("_", StringComparison.Ordinal))
        {
            return null;
        }

        var elasticType = Uri.UnescapeDataString(uri.Segments[1]);

        // multiple indices used, return null to avoid high cardinality
        if (elasticType.Contains(','))
        {
            return null;
        }

        if (elasticType.EndsWith("/", StringComparison.Ordinal))
        {
            elasticType = elasticType.Substring(0, elasticType.Length - 1);
        }

        return elasticType;
    }

    private string ParseAndFormatRequest(string debugInformation)
    {
        if (!this.options.ParseAndFormatRequest)
        {
            return debugInformation;
        }

        var request = ParseRequest.Match(debugInformation);
        if (request.Success)
        {
            string body = request.Groups[1]?.Value?.Trim();
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
}
