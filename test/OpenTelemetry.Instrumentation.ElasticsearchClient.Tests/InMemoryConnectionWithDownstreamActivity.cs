// <copyright file="InMemoryConnectionWithDownstreamActivity.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient.Tests
{
    public class InMemoryConnectionWithDownstreamActivity : InMemoryConnection
    {
        internal static readonly ActivitySource ActivitySource = new ActivitySource("Downstream");
        internal static readonly ActivitySource NestedActivitySource = new ActivitySource("NestedDownstream");

        public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
        {
            using var a1 = ActivitySource.StartActivity("downstream");
            using var a2 = NestedActivitySource.StartActivity("nested-downstream");

            return base.RequestAsync<TResponse>(requestData, cancellationToken);
        }
    }
}
