// <copyright file="StackdriverTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Trace.V2;
using OpenTelemetry.Exporter.Stackdriver.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Stackdriver
{
    public class StackdriverTraceExporter : ActivityExporter
    {
        private readonly Google.Api.Gax.ResourceNames.ProjectName googleCloudProjectId;
        private readonly TraceServiceClient traceClient;

        public StackdriverTraceExporter(string projectId)
        {
            this.googleCloudProjectId = new Google.Api.Gax.ResourceNames.ProjectName(projectId);
            this.traceClient = TraceServiceClient.Create();
        }

        // <inheritdoc/>
        public override async Task<ExportResult> ExportAsync(IEnumerable<Activity> activityList, CancellationToken cancellationToken)
        {
            var spans = activityList.Select(a => a.ToSpan(this.googleCloudProjectId.ProjectId));

            // avoid cancelling here: this is no return point: if we reached this point
            // and cancellation is requested, it's better if we try to finish sending spans rather than drop i
            await this.traceClient.BatchWriteSpansAsync(this.googleCloudProjectId, spans).ConfigureAwait(false);

            // TODO failures
            return ExportResult.Success;
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
