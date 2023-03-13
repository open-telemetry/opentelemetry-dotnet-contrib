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

    [JsonProperty("RuleName")]
    public string RuleName { get; }

    public int Priority { get; }

    public double FixedRate { get; }

    public int ReservoirSize { get; }

    public string Host { get; }

    public string HttpMethod { get; }

    public string ResourceArn { get; }

    public string ServiceName { get; }

    public string UrlPath { get; }

    public int Version { get; }

    public Dictionary<string, string> Attributes { get; }

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
