// <copyright file="SamplingStatisticsDocument.cs" company="OpenTelemetry Authors">
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

using System.Text.Json.Serialization;

namespace OpenTelemetry.Sampler.AWS;

internal class SamplingStatisticsDocument
{
    public SamplingStatisticsDocument(string clientID, string ruleName, long requestCount, long sampledCount, long borrowCount, double timestamp)
    {
        this.ClientID = clientID;
        this.RuleName = ruleName;
        this.RequestCount = requestCount;
        this.SampledCount = sampledCount;
        this.BorrowCount = borrowCount;
        this.Timestamp = timestamp;
    }

    [JsonPropertyName("ClientID")]
    public string ClientID { get; set; }

    [JsonPropertyName("RuleName")]
    public string RuleName { get; set; }

    [JsonPropertyName("RequestCount")]
    public long RequestCount { get; set; }

    [JsonPropertyName("SampledCount")]
    public long SampledCount { get; set; }

    [JsonPropertyName("BorrowCount")]
    public long BorrowCount { get; set; }

    [JsonPropertyName("Timestamp")]
    public double Timestamp { get; set; }
}
