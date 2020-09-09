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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Elasticsearch.Implementation
{
    internal class ElasticsearchRequestPipelineDiagnosticListener
        : ListenerHandler
    {
        public const string ExceptionCustomPropertyName = "OTel.Elasticsearch.Exception";
        public const string AttributeDbSystem = "db.system";
        public const string AttributeDbName = "db.name";
        public const string AttributeNetPeerName = "net.peer.name";
        public const string AttributeNetPeerPort = "net.peer.port";
        public const string AttributeDbStatement = "db.statement";

        private readonly ActivitySourceAdapter activitySource;
        private readonly PropertyFetcher uriFetcher = new PropertyFetcher("Uri");
        private readonly PropertyFetcher methodFetcher = new PropertyFetcher("Method");
        private readonly PropertyFetcher successFetcher = new PropertyFetcher("Success");
        private readonly PropertyFetcher debugInformationFetcher = new PropertyFetcher("DebugInformation");
        private readonly PropertyFetcher httpStatusFetcher = new PropertyFetcher("HttpStatusCode");
        private readonly PropertyFetcher orginalExceptionFetcher = new PropertyFetcher("OriginalException");

        public ElasticsearchRequestPipelineDiagnosticListener(ActivitySourceAdapter activitySource)
            : base("Elasticsearch.Net.RequestPipeline")
        {
            if (activitySource == null)
            {
                throw new ArgumentNullException(nameof(activitySource));
            }

            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            var uri = this.uriFetcher.Fetch(payload) as Uri;

            if (uri == null)
            {
                ElasticsearchInstrumentationEventSource.Log.NullPayload(nameof(ElasticsearchRequestPipelineDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            var method = this.methodFetcher.Fetch(payload)?.ToString();
            var elasticType = uri.Segments.FirstOrDefault(s => !s.Equals("/"));
            if (elasticType != null && elasticType.EndsWith("/"))
            {
                elasticType = elasticType.Substring(0, elasticType.Length - 1);
            }

            if (!string.IsNullOrEmpty(elasticType))
            {
                activity.DisplayName = $"Elasticsearch {method} {elasticType}";
            }
            else
            {
                activity.DisplayName = $"Elasticsearch {method}";
            }

            this.activitySource.Start(activity, ActivityKind.Client);

            // SuppressInstrumentationScope.Enter();

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(AttributeDbSystem, "elasticsearch");
                activity.AddTag(AttributeDbName, elasticType);

                if (!string.IsNullOrEmpty(uri.Host))
                {
                    activity.SetTag(AttributeNetPeerName, uri.Host);
                }

                if (uri.Port > 0)
                {
                    activity.SetTag(AttributeNetPeerPort, uri.Port);
                }
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                var success = this.successFetcher.Fetch(payload) as bool?;
                var statusCode = this.httpStatusFetcher.Fetch(payload) as int?;

                if (statusCode.HasValue && statusCode.Value >= 200 && statusCode.Value < 300)
                {
                    activity.SetStatus(Status.Ok);
                }
                else if (statusCode.HasValue && statusCode.Value == 404)
                {
                    activity.SetStatus(Status.NotFound);
                }
                else if (statusCode.HasValue && statusCode.Value == 401)
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
                    activity.SetTag(AttributeDbStatement, debugInformation);
                }

                var originalException = this.orginalExceptionFetcher.Fetch(payload) as Exception;
                if (originalException != null)
                {
                    activity.SetCustomProperty(ExceptionCustomPropertyName, originalException);

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
