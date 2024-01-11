// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.ResourceDetectors.Container;

internal abstract class ApiConnector : IDisposable
{
    protected const int MaxAttempts = 3;

    protected static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    // TODO add more acceptable response codes?
    // Response code not in this set will cause retries up to MAX_ATTEMPTS
    protected static readonly HashSet<HttpStatusCode> AcceptableResponseCodes =
        [HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden];

    protected static HttpClient httpClient = new();

    protected int connectionTimeout = 5;

    public abstract HttpClientHandler? ClientHandler { get; }

    public abstract Uri Target { get; }

    public string ExecuteRequest()
    {
        string responseString = string.Empty;

        // total of 3 Attempts, max 15s. After 1st attempt wait 5s; After 2nd wait additional 10s
        for (int currentAttempt = 1; currentAttempt <= MaxAttempts; currentAttempt++)
        {
            // no need to wait if last attempt failed
            if (currentAttempt != MaxAttempts)
            {
                // waitTime = lastAttempt(i) * 5s
                Thread.Sleep(TimeSpan.FromSeconds(FiveSeconds.TotalSeconds * currentAttempt));
            }

            responseString = this.ExecuteHttpRequest().Result;

            // responseString = null, would mean a. didn't get 200 Success, OR b. couldn't establish connection
            if (!string.IsNullOrEmpty(responseString))
            {
                break;
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

    public void Dispose()
    {
        this.ClientHandler!.Dispose();
        httpClient.Dispose();
    }
}
