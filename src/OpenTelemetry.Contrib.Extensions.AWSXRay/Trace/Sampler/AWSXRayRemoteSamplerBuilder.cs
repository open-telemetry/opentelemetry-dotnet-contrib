using System;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace.Sampler;

public class AWSXRayRemoteSamplerBuilder
{
    private const long DEFAULT_POLLING_INTERVAL = 300;
    private const string DEFAULT_ENDPOINT = "http://localhost:2000";

    private TimeSpan pollingInterval;
    private string endpoint;

    internal AWSXRayRemoteSamplerBuilder()
    {
        this.pollingInterval = TimeSpan.FromSeconds(DEFAULT_POLLING_INTERVAL);
        this.endpoint = DEFAULT_ENDPOINT;
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
        if (!String.IsNullOrEmpty(endpoint))
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
