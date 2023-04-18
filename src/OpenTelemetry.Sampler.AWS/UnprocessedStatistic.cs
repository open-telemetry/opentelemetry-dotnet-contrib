// <copyright file="UnprocessedStatistic.cs" company="OpenTelemetry Authors">
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

internal class UnprocessedStatistic
{
    public UnprocessedStatistic(string? errorCode, string? message, string? ruleName)
    {
        this.ErrorCode = errorCode;
        this.Message = message;
        this.RuleName = ruleName;
    }

    [JsonPropertyName("ErrorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("RuleName")]
    public string? RuleName { get; set; }
}
