// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Web.Http;
using Examples.AspNet.Models;
using OpenTelemetry;

namespace Examples.AspNet.Controllers;

public class WeatherForecastController : ApiController
{
    private const int DataRequestSizeBytes = 64 * 1024;
    private const int MaxDataRequestBodyBytes = 128 * 1024;

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet] // For testing traditional routing. Ex: https://localhost:XXXX/api/weatherforecast
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        // Build some dependency spans.

        await RequestGoogleHomPageViaHttpClient().ConfigureAwait(false);

        await this.RequestInvalidViaHttpClient().ConfigureAwait(false);

        await this.RequestValidThatReturnsFailedViaHttpClient().ConfigureAwait(false);

        await this.RequestValidThatSpawnsSubSpansViaHttpClient().ConfigureAwait(false);

        return GetWeatherForecast();
    }

    [Route("subroute/{customerId}")] // For testing attribute routing. Ex: https://localhost:XXXX/subroute/10
    [HttpGet]
    public async Task<IEnumerable<WeatherForecast>> Get(int customerId)
    {
        if (customerId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(customerId), "CustomerId should be 0 or greater.");
        }

        // Making http calls here to serve as an example of
        // how dependency calls will be captured and treated
        // automatically as child of incoming request.

        RequestGoogleHomPageViaHttpWebRequestLegacySync();

        await RequestGoogleHomPageViaHttpWebRequestLegacyAsync().ConfigureAwait(false);

        RequestGoogleHomPageViaHttpWebRequestLegacyAsyncResult();

        return GetWeatherForecast();
    }

    /// <summary>
    /// For testing async request/response propagation with a bounded payload size.
    /// </summary>
    [Route("data")]
    [HttpGet]
    public async Task<string> GetData()
    {
        Baggage.SetBaggage("key1", "value1");

        using var rng = RandomNumberGenerator.Create();

        var requestData = new byte[DataRequestSizeBytes];
        rng.GetBytes(requestData);

        using var client = new HttpClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, this.Url.Content("~/data"));

        request.Content = new ByteArrayContent(requestData);

        using var response = await client.SendAsync(request).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        return responseData.SequenceEqual(requestData) ? "match" : "mismatch";
    }

    [Route("data")]
    [HttpPost]
    public async Task<HttpResponseMessage> PostData()
    {
        var value1 = Baggage.GetBaggage("key1");
        if (string.IsNullOrEmpty(value1))
        {
            throw new InvalidOperationException("Key1 was not found on Baggage.");
        }

        var requestBody = await this.ReadRequestBodyAsync(MaxDataRequestBodyBytes).ConfigureAwait(false);

        var result = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(requestBody),
        };

        result.Content.Headers.ContentType = this.Request.Content.Headers.ContentType;

        return result;
    }

    private static WeatherForecast[] GetWeatherForecast()
    {
        var rng = new Random();
        return [.. Enumerable.Range(1, 5).Select(index => new WeatherForecast(
            date: DateTime.Now.AddDays(index),
            temperatureC: rng.Next(-20, 55),
            summary: Summaries[rng.Next(Summaries.Length)]))];
    }

    // Test successful dependency collection via HttpClient.
    private static async Task RequestGoogleHomPageViaHttpClient()
    {
        using var request = new HttpClient();

        using var response = await request.GetAsync(new Uri("http://www.google.com")).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // Test dependency collection via legacy HttpWebRequest sync.
    private static void RequestGoogleHomPageViaHttpWebRequestLegacySync()
    {
        var request = WebRequest.Create(new Uri("http://www.google.com/?sync"));

        using var response = request.GetResponse();
    }

    // Test dependency collection via legacy HttpWebRequest async.
    private static async Task RequestGoogleHomPageViaHttpWebRequestLegacyAsync()
    {
        var request = (HttpWebRequest)WebRequest.Create(new Uri("http://www.google.com/?async"));

        using var response = await request.GetResponseAsync().ConfigureAwait(false);
    }

    // Test dependency collection via legacy HttpWebRequest IAsyncResult.
    private static void RequestGoogleHomPageViaHttpWebRequestLegacyAsyncResult()
    {
        var request = (HttpWebRequest)WebRequest.Create(new Uri("http://www.google.com/?async"));

        var asyncResult = request.BeginGetResponse(null, null);

        using var response = request.EndGetResponse(asyncResult);
    }

    private async Task<byte[]> ReadRequestBodyAsync(int maxAllowedBytes)
    {
        if (this.Request.Content.Headers.ContentLength is long contentLength &&
            contentLength > maxAllowedBytes)
        {
            throw this.CreateHttpResponseException(
                HttpStatusCode.RequestEntityTooLarge,
                $"Request content must be {maxAllowedBytes} bytes or smaller.");
        }

        using var stream = await this.Request.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var buffer = new MemoryStream();

        var readBuffer = new byte[81920];
        var totalBytesRead = 0;

        while (true)
        {
            var read = await stream.ReadAsync(readBuffer, 0, readBuffer.Length).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalBytesRead += read;
            if (totalBytesRead > maxAllowedBytes)
            {
                throw this.CreateHttpResponseException(
                    HttpStatusCode.RequestEntityTooLarge,
                    $"Request content must be {maxAllowedBytes} bytes or smaller.");
            }

            await buffer.WriteAsync(readBuffer, 0, read).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private HttpResponseException CreateHttpResponseException(HttpStatusCode statusCode, string message)
    {
        return new(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(message),
        });
    }

    // Test exception dependency collection via HttpClient.
    private async Task RequestInvalidViaHttpClient()
    {
        try
        {
            using var request = new HttpClient();

            // This request is not available over SSL and will throw a handshake exception.

            var requestUri = this.GenerateContentRequestUri("~/subroute/10", uri => uri.Replace("http", "https"));

            using var response = await request.GetAsync(requestUri).ConfigureAwait(false);

            Debug.Fail("Unreachable");
        }
        catch
        {
        }
    }

    // Test exception dependency collection via HttpClient.
    private async Task RequestValidThatReturnsFailedViaHttpClient()
    {
        using var request = new HttpClient();

        // This request will return a 500 error because customerId should be >= 0;

        using var response = await request.GetAsync(
            this.GenerateContentRequestUri("~/subroute/-1")).ConfigureAwait(false);

        Debug.Assert(response.StatusCode == HttpStatusCode.InternalServerError, "response.StatusCode is InternalServerError");
    }

    // Test successful dependency collection via HttpClient.
    private async Task RequestValidThatSpawnsSubSpansViaHttpClient()
    {
        using var request = new HttpClient();

        // This request will return successfully and cause a bunch of sub-spans;

        using var response = await request.GetAsync(
            this.GenerateContentRequestUri("~/subroute/10")).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    private Uri GenerateContentRequestUri(string path, Func<string, string>? transform = null)
    {
        var rawUri = this.Url.Content(path);

        if (transform != null)
        {
            rawUri = transform(rawUri);
        }

        return new(rawUri);
    }
}
