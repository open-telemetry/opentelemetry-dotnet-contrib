using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
internal class AWSXRaySamplerClient
{
    private readonly string getSamplingRulesEndpoint;
    private readonly HttpClient httpClient;

    public AWSXRaySamplerClient(string host)
    {
        this.getSamplingRulesEndpoint = host + "/GetSamplingRules";
        this.httpClient = new HttpClient();
        this.httpClient.DefaultRequestHeaders.Accept.Clear();
        this.httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<SamplingRule>> GetSamplingRules()
    {
        var responseTask = await this.DoRequest(this.getSamplingRulesEndpoint);
        var response = responseTask.Content.ReadAsStringAsync().Result;

        var getSamplingRulesResponse = JsonConvert.DeserializeObject<GetSamplingRulesResponse>(response);

        List<SamplingRule> samplingRules = new List<SamplingRule>();
        foreach (var samplingRuleRecord in getSamplingRulesResponse.SamplingRuleRecords)
        {
            samplingRules.Add(samplingRuleRecord.SamplingRule);
        }

        return samplingRules;
    }

    private async Task<HttpResponseMessage> DoRequest(string endpoint)
    {
        var values = new Dictionary<string, string>
        {
            { "NextToken", "null" },
        };

        var content = new FormUrlEncodedContent(values);
        var response = await this.httpClient.PostAsync(endpoint, content);
        return response;
    }
}
