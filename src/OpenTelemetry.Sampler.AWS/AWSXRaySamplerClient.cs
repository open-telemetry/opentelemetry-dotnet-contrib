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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenTelemetry.Sampler.AWS;

internal class AWSXRaySamplerClient : IDisposable
{
    private readonly string getSamplingRulesEndpoint;
    private readonly string getSamplingTargetsEndpoint;

    private readonly HttpClient httpClient;
    private readonly string jsonContentType = "application/json";

    public AWSXRaySamplerClient(string host)
    {
        this.getSamplingRulesEndpoint = host + "/GetSamplingRules";
        this.getSamplingTargetsEndpoint = host + "/SamplingTargets";
        this.httpClient = new HttpClient();
    }

    public async Task<List<SamplingRule>> GetSamplingRules()
    {
        List<SamplingRule> samplingRules = new List<SamplingRule>();

        using (var request = new HttpRequestMessage(HttpMethod.Post, this.getSamplingRulesEndpoint)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, this.jsonContentType),
        })
        {
            var responseJson = await this.DoRequestAsync(this.getSamplingRulesEndpoint, request).ConfigureAwait(false);

            try
            {
                GetSamplingRulesResponse? getSamplingRulesResponse = JsonSerializer.Deserialize<GetSamplingRulesResponse>(responseJson);
                if (getSamplingRulesResponse is not null)
                {
                    if (getSamplingRulesResponse.SamplingRuleRecords is not null)
                    {
                        foreach (var samplingRuleRecord in getSamplingRulesResponse.SamplingRuleRecords)
                        {
                            if (samplingRuleRecord.SamplingRule is not null)
                            {
                                samplingRules.Add(samplingRuleRecord.SamplingRule);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AWSSamplerEventSource.Log.FailedToDeserializeResponse(
                    nameof(this.GetSamplingRules),
                    ex.Message);
            }
        }

        return samplingRules;
    }

    public async Task<GetSamplingTargetsResponse?> GetSamplingTargets(GetSamplingTargetsRequest getSamplingTargetsRequest)
    {
        var content = new StringContent(JsonSerializer.Serialize(getSamplingTargetsRequest), Encoding.UTF8, this.jsonContentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, this.getSamplingTargetsEndpoint)
        {
            Content = content,
        };

        var responseJson = await this.DoRequestAsync(this.getSamplingTargetsEndpoint, request).ConfigureAwait(false);

        try
        {
            GetSamplingTargetsResponse? getSamplingTargetsResponse = JsonSerializer
                .Deserialize<GetSamplingTargetsResponse>(responseJson);

            return getSamplingTargetsResponse;
        }
        catch (Exception ex)
        {
            AWSSamplerEventSource.Log.FailedToDeserializeResponse(
                    nameof(this.GetSamplingTargets),
                    ex.Message);
        }

        return null;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.httpClient?.Dispose();
        }
    }

    private async Task<string> DoRequestAsync(string endpoint, HttpRequestMessage request)
    {
        try
        {
            var response = await this.httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                AWSSamplerEventSource.Log.FailedToGetSuccessResponse(endpoint, response.StatusCode.ToString());
                return string.Empty;
            }

            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return responseString;
        }
        catch (Exception ex)
        {
            AWSSamplerEventSource.Log.ExceptionFromSampler(ex.Message);
            return string.Empty;
        }
    }
}
