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
        private static readonly Regex ParseRequest = new Regex(@"\n# Request:\r?\n(.*)\n# Response", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly ActivitySourceAdapter activitySource;
        private readonly ElasticsearchClientInstrumentationOptions options;
        private readonly PropertyFetcher<Uri> uriFetcher = new PropertyFetcher<Uri>("Uri");
        private readonly PropertyFetcher<object> methodFetcher = new PropertyFetcher<object>("Method");
        private readonly PropertyFetcher<bool> successFetcher = new PropertyFetcher<bool>("SuccessOrKnownError");
        private readonly PropertyFetcher<string> debugInformationFetcher = new PropertyFetcher<string>("DebugInformation");
        private readonly PropertyFetcher<int?> httpStatusFetcher = new PropertyFetcher<int?>("HttpStatusCode");
        private readonly PropertyFetcher<Exception> orginalExceptionFetcher = new PropertyFetcher<Exception>("OriginalException");

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

            var method = this.methodFetcher.Fetch(payload)?.ToString();

            if (activity.OperationName == "Ping")
            {
                activity.DisplayName = $"Elasticsearch: Ping";
            }
            else
            {
                activity.DisplayName = $"Elasticsearch: {method} {uri.AbsolutePath}";
            }

            this.activitySource.Start(activity, ActivityKind.Client);

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();
            }

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(Constants.AttributeDbSystem, "elasticsearch");

                var elasticType = uri.Segments.FirstOrDefault(s => !s.Equals("/") && !s.StartsWith("_"));
                if (elasticType != null && elasticType.EndsWith("/"))
                {
                    elasticType = elasticType.Substring(0, elasticType.Length - 1);
                }

                activity.AddTag(Constants.AttributeDbName, elasticType);

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

                activity.SetTag(Constants.AttributeDbUrl,  uri.OriginalString);
                activity.SetTag(Constants.AttributeDbMethod, method);
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                // TODO: Seems like we should be using this value
                var success = this.successFetcher.Fetch(payload);
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
                else
                {
                    activity.SetStatus(Status.Unknown);
                }

                var debugInformation = this.debugInformationFetcher.Fetch(payload);
                if (debugInformation != null)
                {
                    activity.SetTag(Constants.AttributeDbStatement, this.ParseAndFormatRequest(activity, debugInformation));
                }

                var originalException = this.orginalExceptionFetcher.Fetch(payload);
                if (originalException != null)
                {
                    activity.SetCustomProperty(Constants.ExceptionCustomPropertyName, originalException);

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

        private string ParseAndFormatRequest(Activity activity, string debugInformation)
        {
            if (!this.options.ParseAndFormatRequest)
            {
                return debugInformation;
            }

            var request = ParseRequest.Match(debugInformation);
            if (request.Success)
            {
                string method = activity.GetTagValue(Constants.AttributeDbMethod).ToString();
                string url = activity.GetTagValue(Constants.AttributeDbUrl).ToString();
                string body = request.Groups[1].Value.Trim();

                var doc = JsonDocument.Parse(body);
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
