// <copyright file="SamplingRule.cs" company="OpenTelemetry Authors">
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
using Newtonsoft.Json;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
internal class SamplingRule : IComparable<SamplingRule>
{
    public SamplingRule(
        string ruleName,
        int priority,
        double fixedRate,
        int reservoirSize,
        string host,
        string httpMethod,
        string resourceArn,
        string serviceName,
        string urlPath,
        int version,
        Dictionary<string, string> attributes)
    {
        this.RuleName = ruleName;
        this.Priority = priority;
        this.FixedRate = fixedRate;
        this.ReservoirSize = reservoirSize;
        this.Host = host;
        this.HttpMethod = httpMethod;
        this.ResourceArn = resourceArn;
        this.ServiceName = serviceName;
        this.UrlPath = urlPath;
        this.Version = version;
        this.Attributes = attributes;
    }

    [JsonProperty(nameof(RuleName))]
    public string RuleName { get; set; }

    [JsonProperty(nameof(Priority))]
    public int Priority { get; set; }

    [JsonProperty(nameof(FixedRate))]
    public double FixedRate { get; set; }

    [JsonProperty(nameof(ReservoirSize))]
    public int ReservoirSize { get; set; }

    [JsonProperty(nameof(Host))]
    public string Host { get; set; }

    [JsonProperty(nameof(HttpMethod))]
    public string HttpMethod { get; set; }

    [JsonProperty(nameof(ResourceArn))]
    public string ResourceArn { get; set; }

    [JsonProperty(nameof(ServiceName))]
    public string ServiceName { get; set; }

    [JsonProperty(nameof(UrlPath))]
    public string UrlPath { get; set; }

    [JsonProperty(nameof(Version))]
    public int Version { get; set; }

    [JsonProperty(nameof(Attributes))]
    public Dictionary<string, string>? Attributes { get; set; }

    public int CompareTo(SamplingRule other)
    {
        int result = this.Priority.CompareTo(other.Priority);
        if (result == 0)
        {
            result = this.RuleName.CompareTo(other.RuleName);
        }

        return result;
    }
}
