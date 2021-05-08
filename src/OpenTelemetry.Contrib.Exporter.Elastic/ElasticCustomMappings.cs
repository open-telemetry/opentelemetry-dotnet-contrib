// <copyright file="ElasticCustomMappings.cs" company="OpenTelemetry Authors">
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
    /// Custom mappings for transactions and spans.
    /// </summary>
    public class ElasticCustomMappings
    {
        /// <summary>
        /// Gets or sets custom mapping for Elastic APM transaction result. Default OTel StatusCode with Ok for Http Success status code.
        /// https://github.com/elastic/apm-server/blob/32f34ed4298d648bf9476790f2a8a54d72805bb6/docs/spec/v2/transaction.json#L680.
        /// </summary>
        public Func<HttpStatusCode?, StatusCode?, string> TransactionResult { get; set; } =
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
    }
}
