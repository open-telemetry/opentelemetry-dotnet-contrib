﻿// <copyright file="ElasticsearchClientInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Elasticsearch.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Elasticsearch
{
    /// <summary>
    /// ElasticsearchClient instrumentation.
    /// </summary>
    internal class ElasticsearchClientInstrumentation : IDisposable
    {
        private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElasticsearchClientInstrumentation"/> class.
        /// </summary>
        /// <param name="activitySource">ActivitySource adapter instance.</param>
        public ElasticsearchClientInstrumentation(ActivitySourceAdapter activitySource)
        {
            if (activitySource == null)
            {
                throw new ArgumentNullException(nameof(activitySource));
            }

            this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new ElasticsearchRequestPipelineDiagnosticListener(activitySource), null);
            this.diagnosticSourceSubscriber.Subscribe();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.diagnosticSourceSubscriber.Dispose();
        }
    }
}
