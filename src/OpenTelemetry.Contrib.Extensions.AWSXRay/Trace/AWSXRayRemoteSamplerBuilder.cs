using System;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;

public class AWSXRayRemoteSamplerBuilder
{
    private const long DefaultPollingInterval = 300;
    private const string DefaultEndpoint = "http://localhost:2000";

    private TimeSpan pollingInterval;
    private string endpoint;

    internal AWSXRayRemoteSamplerBuilder()
    {
        this.pollingInterval = TimeSpan.FromSeconds(DefaultPollingInterval);
        this.endpoint = DefaultEndpoint;
    }

    public AWSXRayRemoteSamplerBuilder SetPollingInterval(TimeSpan pollingInterval)
    {
        if (pollingInterval != null)
        {
            if (pollingInterval < TimeSpan.Zero)
            {
                throw new ArgumentException("Polling interval must be non-negative.");
            }

            this.pollingInterval = pollingInterval;
        }

        return this;
    }

    public AWSXRayRemoteSamplerBuilder SetEndpoint(string endpoint)
    {
        if (!string.IsNullOrEmpty(endpoint))
        {
            this.endpoint = endpoint;
        }

        return this;
    }

    public AWSXRayRemoteSampler Build()
    {
        return new AWSXRayRemoteSampler(this.pollingInterval, this.endpoint);
    }
}
