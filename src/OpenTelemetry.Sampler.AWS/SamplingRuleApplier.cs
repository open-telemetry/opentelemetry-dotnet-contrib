// <copyright file="SamplingRuleApplier.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class SamplingRuleApplier
{
    public SamplingRuleApplier(string clientId, Clock clock, SamplingRule rule, Statistics statistics)
    {
        this.ClientId = clientId;
        this.Clock = clock;
        this.Rule = rule;
        this.RuleName = this.Rule.RuleName;
        this.Statistics = statistics ?? new Statistics();
    }

    internal string ClientId { get; set; }

    internal SamplingRule Rule { get; set; }

    internal string RuleName { get; set; }

    internal Clock Clock { get; set; }

    internal Statistics Statistics { get; set; }

    // check if this rule applier matches the request
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

        return Matcher.AttributeMatch(samplingParameters.Tags, this.Rule.Attributes) &&
               Matcher.WildcardMatch(httpTarget, this.Rule.UrlPath) &&
               Matcher.WildcardMatch(httpMethod, this.Rule.HttpMethod) &&
               Matcher.WildcardMatch(httpHost, this.Rule.Host) &&
               Matcher.WildcardMatch(serviceName, this.Rule.ServiceName) &&
               Matcher.WildcardMatch(GetServiceType(resource), this.Rule.ServiceType) &&
               Matcher.WildcardMatch(GetArn(in samplingParameters, resource), this.Rule.ResourceArn);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "method work in progress")]
    public SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // for now return drop sampling result.
        // TODO: use reservoir and fixed rate sampler
        return new SamplingResult(false);
    }

    private static string GetServiceType(Resource resource)
    {
        string cloudPlatform = (string)resource.Attributes.FirstOrDefault(kvp =>
            kvp.Key.Equals("cloud.platform", StringComparison.Ordinal)).Value;

        if (cloudPlatform == null)
        {
            return string.Empty;
        }

        return Matcher.XRayCloudPlatform.TryGetValue(cloudPlatform, out string? value) ? value : string.Empty;
    }

    private static string GetArn(in SamplingParameters samplingParameters, Resource resource)
    {
        // currently the aws resource detectors only capture ARNs for ECS and Lambda environments.
        string? arn = (string?)resource.Attributes.FirstOrDefault(kvp =>
            kvp.Key.Equals("aws.ecs.container.arn", StringComparison.Ordinal)).Value;

        if (arn != null)
        {
            return arn;
        }

        if (GetServiceType(resource).Equals("AWS::Lambda::Function", StringComparison.Ordinal))
        {
            arn = (string?)samplingParameters.Tags?.FirstOrDefault(kvp => kvp.Key.Equals("faas.id", StringComparison.Ordinal)).Value;

            if (arn != null)
            {
                return arn;
            }
        }

        return string.Empty;
    }
}
