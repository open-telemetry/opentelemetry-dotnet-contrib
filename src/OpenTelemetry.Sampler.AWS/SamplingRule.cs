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
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class SamplingRule : IComparable<SamplingRule>, IDisposable
{
    private readonly ReaderWriterLockSlim rwLock;

    public SamplingRule(
        string ruleName,
        int priority,
        double fixedRate,
        int reservoirSize,
        string host,
        string httpMethod,
        string resourceArn,
        string serviceName,
        string serviceType,
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
        this.ServiceType = serviceType;
        this.UrlPath = urlPath;
        this.Version = version;
        this.Attributes = attributes;

        this.Reservoir = new Reservoir(this.ReservoirSize);
        this.Statistics = new Statistics();

        this.rwLock = new ReaderWriterLockSlim();
    }

    [JsonPropertyName("RuleName")]
    public string RuleName { get; set; }

    [JsonPropertyName("Priority")]
    public int Priority { get; set; }

    [JsonPropertyName("FixedRate")]
    public double FixedRate { get; set; }

    [JsonPropertyName("ReservoirSize")]
    public int ReservoirSize { get; set; }

    [JsonPropertyName("Host")]
    public string Host { get; set; }

    [JsonPropertyName("HTTPMethod")]
    public string HttpMethod { get; set; }

    [JsonPropertyName("ResourceARN")]
    public string ResourceArn { get; set; }

    [JsonPropertyName("ServiceName")]
    public string ServiceName { get; set; }

    [JsonPropertyName("ServiceType")]
    public string ServiceType { get; set; }

    [JsonPropertyName("URLPath")]
    public string UrlPath { get; set; }

    [JsonPropertyName("Version")]
    public int Version { get; set; }

    [JsonPropertyName("Attributes")]
    public Dictionary<string, string> Attributes { get; set; }

    public Reservoir Reservoir { get; internal set; }

    public Statistics Statistics { get; internal set; }

    public bool Matches(SamplingParameters samplingParameters, Resource resource)
    {
        string? httpTarget = null;
        string? httpUrl = null;
        string? httpMethod = null;
        string? httpHost = null;

        if (samplingParameters.Tags is not null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (tag.Key.Equals(SemanticConventions.AttributeHttpTarget, StringComparison.Ordinal))
                {
                    httpTarget = (string?)tag.Value;
                }
                else if (tag.Key.Equals(SemanticConventions.AttributeHttpUrl, StringComparison.Ordinal))
                {
                    httpUrl = (string?)tag.Value;
                }
                else if (tag.Key.Equals(SemanticConventions.AttributeHttpMethod, StringComparison.Ordinal))
                {
                    httpMethod = (string?)tag.Value;
                }
                else if (tag.Key.Equals(SemanticConventions.AttributeHttpHost, StringComparison.Ordinal))
                {
                    httpHost = (string?)tag.Value;
                }
            }
        }

        // URL path may be in either http.target or http.url
        if (httpTarget == null && httpUrl != null)
        {
            int schemeEndIndex = httpUrl.IndexOf("://", StringComparison.Ordinal);

            // Per spec, http.url is always populated with scheme://host/target. If scheme doesn't
            // match, assume it's bad instrumentation and ignore.
            if (schemeEndIndex > 0)
            {
                int pathIndex = httpUrl.IndexOf('/', schemeEndIndex + "://".Length);
                if (pathIndex < 0)
                {
                    httpTarget = "/";
                }
                else
                {
                    httpTarget = httpUrl.Substring(pathIndex);
                }
            }
        }

        string serviceName = (string)resource.Attributes.FirstOrDefault(kvp =>
                kvp.Key.Equals("service.name", StringComparison.Ordinal)).Value;

        return Matcher.AttributeMatch(samplingParameters.Tags, this.Attributes) &&
               Matcher.WildcardMatch(httpTarget, this.UrlPath) &&
               Matcher.WildcardMatch(httpMethod, this.HttpMethod) &&
               Matcher.WildcardMatch(httpHost, this.Host) &&
               Matcher.WildcardMatch(serviceName, this.ServiceName) &&
               Matcher.WildcardMatch(GetServiceType(resource), this.ServiceType);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "method work in progress")]
    public SamplingResult Sample(SamplingParameters samplingParameters)
    {
        // For now returning a drop sample decision.
        // TODO: use reservoir to make a sampling decision.
        return new SamplingResult(false);
    }

    public int CompareTo(SamplingRule? other)
    {
        int result = this.Priority.CompareTo(other?.Priority);
        if (result == 0)
        {
            result = string.Compare(this.RuleName, other?.RuleName, StringComparison.Ordinal);
        }

        return result;
    }

    public SamplingRule DeepCopy()
    {
        this.rwLock.EnterReadLock();
        try
        {
            return new SamplingRule(
                this.RuleName,
                this.Priority,
                this.FixedRate,
                this.ReservoirSize,
                this.Host,
                this.HttpMethod,
                this.ResourceArn,
                this.ServiceName,
                this.ServiceType,
                this.UrlPath,
                this.Version,
                this.Attributes = new Dictionary<string, string>(this.Attributes))
            {
                Reservoir = this.Reservoir.DeepCopy(),
                Statistics = this.Statistics.DeepCopy(),
            };
        }
        finally
        {
            this.rwLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static string GetServiceType(Resource resource)
    {
        string cloudPlatform = (string)resource.Attributes.FirstOrDefault(kvp =>
            kvp.Key.Equals("cloud.platform", StringComparison.Ordinal)).Value;

        return Matcher.XRayCloudPlatform.TryGetValue(cloudPlatform, out string? value) ? value : string.Empty;
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.rwLock.Dispose();
        }
    }
}
