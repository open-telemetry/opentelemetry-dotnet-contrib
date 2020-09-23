﻿// <copyright file="ElasticsearchRequestPipelineDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient.Implementation
{
    internal class ElasticsearchRequestPipelineDiagnosticListener : ListenerHandler
    {
        private static readonly Regex ParseRequest = new Regex(@"\n# Request:\r?\n(\{.*)\n# Response", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly ConcurrentDictionary<object, string> MethodNameCache = new ConcurrentDictionary<object, string>();

        private readonly ActivitySourceAdapter activitySource;
        private readonly ElasticsearchClientInstrumentationOptions options;
        private readonly MultiTypePropertyFetcher<Uri> uriFetcher = new MultiTypePropertyFetcher<Uri>("Uri");
        private readonly MultiTypePropertyFetcher<object> methodFetcher = new MultiTypePropertyFetcher<object>("Method");
        private readonly MultiTypePropertyFetcher<string> debugInformationFetcher = new MultiTypePropertyFetcher<string>("DebugInformation");
        private readonly MultiTypePropertyFetcher<int?> httpStatusFetcher = new MultiTypePropertyFetcher<int?>("HttpStatusCode");
        private readonly MultiTypePropertyFetcher<Exception> originalExceptionFetcher = new MultiTypePropertyFetcher<Exception>("OriginalException");
        private readonly MultiTypePropertyFetcher<object> failureReasonFetcher = new MultiTypePropertyFetcher<object>("FailureReason");
        private readonly MultiTypePropertyFetcher<byte[]> responseBodyFetcher = new MultiTypePropertyFetcher<byte[]>("ResponseBodyInBytes");

        public ElasticsearchRequestPipelineDiagnosticListener(ActivitySourceAdapter activitySource, ElasticsearchClientInstrumentationOptions options)
            : base("Elasticsearch.Net.RequestPipeline")
        {
            if (activitySource == null)
            {
                throw new ArgumentNullException(nameof(activitySource));
            }

            this.activitySource = activitySource;
            this.options = options;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            var uri = this.uriFetcher.Fetch(payload);

            if (uri == null)
            {
                ElasticsearchInstrumentationEventSource.Log.NullPayload(nameof(ElasticsearchRequestPipelineDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            var method = this.methodFetcher.Fetch(payload);
            activity.DisplayName = this.GetDisplayName(activity, method);

            this.activitySource.Start(activity, ActivityKind.Client);

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();
            }

            if (!activity.IsAllDataRequested)
            {
                return;
            }

            var elasticIndex = this.GetElasticIndex(uri);
            activity.DisplayName = this.GetDisplayName(activity, method, elasticIndex);
            activity.SetTag(Constants.AttributeDbSystem, "elasticsearch");

            if (elasticIndex != null)
            {
                activity.SetTag(Constants.AttributeDbName, elasticIndex);
            }

            var uriHostNameType = Uri.CheckHostName(uri.Host);
            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                activity.SetTag(Constants.AttributeNetPeerIp, uri.Host);
            }
            else
            {
                activity.SetTag(Constants.AttributeNetPeerName, uri.Host);
            }

            if (uri.Port > 0)
            {
                activity.SetTag(Constants.AttributeNetPeerPort, uri.Port);
            }

            if (method != null)
            {
                activity.SetTag(Constants.AttributeDbMethod, method.ToString());
            }

            activity.SetTag(Constants.AttributeDbUrl, uri.OriginalString);
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                var statusCode = this.httpStatusFetcher.Fetch(payload);

                if (!statusCode.HasValue)
                {
                    activity.SetStatus(Status.Unknown);
                }
                else if (statusCode >= 200 && statusCode < 300)
                {
                    activity.SetStatus(Status.Ok);
                }
                else if (statusCode == 404)
                {
                    activity.SetStatus(Status.NotFound);
                }
                else if (statusCode == 401)
                {
                    activity.SetStatus(Status.Unauthenticated);
                }
                else if (statusCode == 400)
                {
                    activity.SetStatus(Status.AlreadyExists);
                }
                else
                {
                    activity.SetStatus(Status.Unknown);
                }

                var debugInformation = this.debugInformationFetcher.Fetch(payload);
                if (debugInformation != null)
                {
                    activity.SetTag(Constants.AttributeDbStatement, this.ParseAndFormatRequest(activity, debugInformation));
                }

                var originalException = this.originalExceptionFetcher.Fetch(payload);
                if (originalException != null)
                {
                    activity.SetCustomProperty(Constants.ExceptionCustomPropertyName, originalException);

                    var failureReason = this.failureReasonFetcher.Fetch(originalException);
                    if (failureReason != null)
                    {
                        activity.SetStatus(Status.Unknown.WithDescription($"{failureReason} {originalException.Message}"));
                    }

                    var responseBody = this.responseBodyFetcher.Fetch(payload);
                    if (responseBody != null && responseBody.Length > 0)
                    {
                        var response = Encoding.UTF8.GetString(responseBody);
                        activity.SetStatus(Status.Unknown.WithDescription($"{failureReason} {originalException.Message}\r\n{response}"));
                    }

                    if (originalException is HttpRequestException)
                    {
                        if (originalException.InnerException is SocketException exception)
                        {
                            switch (exception.SocketErrorCode)
                            {
                                case SocketError.HostNotFound:
                                    activity.SetStatus(Status.InvalidArgument.WithDescription(originalException.Message));
                                    return;
                            }
                        }

                        if (originalException.InnerException != null)
                        {
                            activity.SetStatus(Status.Unknown.WithDescription(originalException.Message));
                        }
                    }
                }
            }

            this.activitySource.Stop(activity);
        }

        private string GetDisplayName(Activity activity, object method, string elasticType = null)
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

        private string GetElasticIndex(Uri uri)
        {
            // first segment is always /
            if (uri.Segments.Length < 2)
            {
                return null;
            }

            // operations starting with _ are not indices (_cat, _search, etc)
            if (uri.Segments[1].StartsWith("_"))
            {
                return null;
            }

            var elasticType = Uri.UnescapeDataString(uri.Segments[1]);

            // multiple indices used, return null to avoid high cardinality
            if (elasticType.Contains(','))
            {
                return null;
            }

            if (elasticType.EndsWith("/"))
            {
                elasticType = elasticType.Substring(0, elasticType.Length - 1);
            }

            return elasticType;
        }

        private string ParseAndFormatRequest(Activity activity, string debugInformation)
        {
            if (!this.options.ParseAndFormatRequest)
            {
                return debugInformation;
            }

            string method = activity.GetTagValue(Constants.AttributeDbMethod).ToString();
            string url = activity.GetTagValue(Constants.AttributeDbUrl).ToString();

            if (method == "GET")
            {
                return $"GET {url}";
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

                using (var stream = new MemoryStream())
                {
                    var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                    doc.WriteTo(writer);
                    writer.Flush();
                    body = Encoding.UTF8.GetString(stream.ToArray());
                }

                return $"{method} {url}\r\n{body}";
            }

            return debugInformation;
        }
    }
}
