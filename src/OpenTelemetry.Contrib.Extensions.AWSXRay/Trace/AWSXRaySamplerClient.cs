// <copyright file="AWSXRaySamplerClient.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
internal class AWSXRaySamplerClient : IDisposable
{
    private readonly string getSamplingRulesEndpoint;
    private readonly HttpClient httpClient;
    private readonly string jsonContentType = "application/json";

    public AWSXRaySamplerClient(string host)
    {
        this.getSamplingRulesEndpoint = host + "/GetSamplingRules";
        this.httpClient = new HttpClient();
    }

    public async Task<List<SamplingRule>> GetSamplingRules()
    {
        List<SamplingRule> samplingRules = new List<SamplingRule>();

        var request = new HttpRequestMessage(HttpMethod.Post, this.getSamplingRulesEndpoint)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, this.jsonContentType),
        };

        var responseJson = await this.DoRequestAsync(this.getSamplingRulesEndpoint, request).ConfigureAwait(false);

        try
        {
            var getSamplingRulesResponse = JsonConvert.DeserializeObject<GetSamplingRulesResponse>(responseJson);
            if (getSamplingRulesResponse != null)
            {
                foreach (var samplingRuleRecord in getSamplingRulesResponse.SamplingRuleRecords)
                {
                    samplingRules.Add(samplingRuleRecord.SamplingRule);
                }

                // TODO: this line here is only for testing. Remove in next more complete iterations.
                Console.WriteLine("Got sampling rules! Count: " + samplingRules.Count);
            }
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.FailedToDeserializeResponse(nameof(AWSXRaySamplerClient.GetSamplingRules), ex.Message);
        }

        return samplingRules;
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    private async Task<string> DoRequestAsync(string endpoint, HttpRequestMessage request)
    {
        try
        {
            var response = await this.httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                AWSXRayEventSource.Log.FailedToGetSuccessResponse(endpoint, response.StatusCode.ToString());
                return string.Empty;
            }

            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return responseString;
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ExceptionFromSampler(ex.Message);
            return string.Empty;
        }
    }
}
