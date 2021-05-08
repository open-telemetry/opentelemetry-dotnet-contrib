// <copyright file="ElasticOptions.cs" company="OpenTelemetry Authors">
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
using System.Net;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    /// <summary>
    /// Elastic APM exporter options.
    /// </summary>
    public class ElasticOptions
    {
        /// <summary>
        /// Gets or sets Elastic APM Server host. Default value: http://localhost:8200/.
        /// https://www.elastic.co/guide/en/apm/server/current/configuration-process.html#host.
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:8200/";

        /// <summary>
        /// Gets or sets application environment. Default value: Dev.
        /// </summary>
        public string Environment { get; set; } = "Dev";

        /// <summary>
        /// Gets or sets application name. Default value: MyService.
        /// </summary>
        public string ServiceName { get; set; } = "MyService";

        /// <summary>
        /// Gets or sets custom mapping for Elastic APM transaction result. Default OTel StatusCode with Ok for Http Success status code.
        /// https://github.com/elastic/apm-server/blob/32f34ed4298d648bf9476790f2a8a54d72805bb6/docs/spec/v2/transaction.json#L680.
        /// </summary>
        public Func<HttpStatusCode?, StatusCode?, string> TransactionResultMapping { get; set; } =
            (httpStatusCode, otelStatusCode) => otelStatusCode switch
            {
                StatusCode.Ok => "Ok",
                StatusCode.Error => "Error",
                StatusCode.Unset => httpStatusCode.HasValue
                    ? ((int)httpStatusCode >= 200) && ((int)httpStatusCode <= 299)
                        ? "Ok"
                        : "Error"
                    : "Unset",
                _ => "Unknown"
            };

        /// <summary>
        /// Gets or sets Elastic APM Server API version. Default value: IntakeApiVersion.V2.
        /// https://www.elastic.co/guide/en/apm/server/current/events-api.html#events-api-endpoint.
        /// </summary>
        public IntakeApiVersion IntakeApiVersion { get; set; } = IntakeApiVersion.V2;
    }
}
