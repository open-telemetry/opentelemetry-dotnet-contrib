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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient.Implementation
{
    internal class ElasticsearchRequestPipelineDiagnosticListener : ListenerHandler
    {
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
            var elasticType = uri.Segments.FirstOrDefault(s => !s.Equals("/") && !s.StartsWith("_"));
            if (elasticType != null && elasticType.EndsWith("/"))
            {
                elasticType = elasticType.Substring(0, elasticType.Length - 1);
            }

            if (!string.IsNullOrEmpty(elasticType))
            {
                activity.DisplayName = $"Elasticsearch {method} {elasticType}";
            }
            else if (activity.OperationName == "Ping")
            {
                activity.DisplayName = $"Elasticsearch Ping";
            }
            else
            {
                activity.DisplayName = $"Elasticsearch {method}";
            }

            this.activitySource.Start(activity, ActivityKind.Client);

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();
            }

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(Constants.AttributeDbSystem, "elasticsearch");
                activity.AddTag(Constants.AttributeDbName, elasticType);

                if (!string.IsNullOrEmpty(uri.Host))
                {
                    activity.SetTag(Constants.AttributeNetPeerName, uri.Host);
                }

                if (uri.Port > 0)
                {
                    activity.SetTag(Constants.AttributeNetPeerPort, uri.Port);
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
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
                    activity.SetTag(Constants.AttributeDbStatement, debugInformation);
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
    }
}
