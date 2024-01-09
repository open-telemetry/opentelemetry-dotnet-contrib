using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace OpenTelemetry.ResourceDetectors.Container;

internal abstract class ApiConnector
{
    protected int ConnectionTimeout = 5000;

    protected static readonly int MAX_ATTEMPTS = 3;
    protected static readonly TimeSpan FIVE_SECONDS = TimeSpan.FromSeconds(5);

    // TODO add more acceptable response codes?
    // Response code not in this set will cause retries upto MAX_ATTEMPTS
    protected static readonly HashSet<HttpStatusCode> ACCEPTABLE_RESPONSE_CODES =
        new() { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden };

    // Create custom Apache Http Client. Just like we are doing in MTAgent
    // SimpleHttpClientWrapper (from controller api) doesn't provide a way to create custom SSLContext.

    public abstract Uri Target { get; }

    public string ExecuteRequest()
    {
        string responseString = string.Empty;

        // total of 3 Attempts, max 15s. After 1st attempt wait 5s; After 2nd wait additional 10s
        // TODO will this interfere with app agent startup timeout?
        for (int currentAttempt = 1; currentAttempt <= MAX_ATTEMPTS; currentAttempt++)
        {
            responseString = this.ExecuteHttpRequest(currentAttempt);

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

    public string ExecuteHttpRequest(int currentAttempt)
    {
        string responseString = string.Empty;
        Uri uri = this.Target;
        // Log.Info($"Executing request (attempt: {currentAttempt}) {uri}");

        try
        {
            //Communicator.Send(
            //    new byte[0],
            //    uri,
            //    "GET",
            //    "application/json",
            //    new Dictionary<string, string> { { "Accept", "application/json" } },
            //    null,
            //    null, (statusCode, stream) =>
            //    {
            //        if (ACCEPTABLE_RESPONSE_CODES.Contains(statusCode))
            //        {
            //            var readStream = new StreamReader(stream, Encoding.UTF8);
            //            responseString = readStream.ReadToEnd();
            //            return true;
            //        }

            //        return true;
            //    }, ConnectionTimeout);

            return responseString;

        }
        catch (WebException e)
        {
            // Log.Warn(e, "Container ID API request failed");
            return null;
        }
    }
}
