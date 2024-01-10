using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.ResourceDetectors.Container;

internal abstract class ApiConnector
{
    protected int connectionTimeout = 5000;

    protected static readonly int MAX_ATTEMPTS = 3;
    protected static readonly TimeSpan FIVE_SECONDS = TimeSpan.FromSeconds(5);

    // TODO add more acceptable response codes?
    // Response code not in this set will cause retries upto MAX_ATTEMPTS
    protected static readonly HashSet<HttpStatusCode> ACCEPTABLE_RESPONSE_CODES =
        new() { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden };

    protected static HttpClientHandler httpClientHandler = new();
    protected static HttpClient httpClient = new();

    public abstract Uri Target { get; }

    public string ExecuteRequest()
    {
        string responseString = string.Empty;

        // total of 3 Attempts, max 15s. After 1st attempt wait 5s; After 2nd wait additional 10s
        // TODO will this interfere with app agent startup timeout?
        for (int currentAttempt = 1; currentAttempt <= MAX_ATTEMPTS; currentAttempt++)
        {
            responseString = this.ExecuteHttpRequest().Result;

            // responseString = null, would mean a. didn't get 200 Success, OR b. couldn't establish connection
            if (!string.IsNullOrEmpty(responseString))
            {
                break;
            }

            // no need to wait if last attempt failed
            if (currentAttempt != MAX_ATTEMPTS)
            {
                // waitTime = lastAttempt(i) * 5s
                Thread.Sleep(TimeSpan.FromSeconds(FIVE_SECONDS.TotalSeconds * currentAttempt));
            }
        }

        return responseString;
    }

    public async Task<string> ExecuteHttpRequest()
    {
        Uri uri = this.Target;

        try
        {
            httpClient.Timeout = TimeSpan.FromSeconds(this.connectionTimeout);
            using HttpResponseMessage response = await httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return responseBody;

        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }
}
