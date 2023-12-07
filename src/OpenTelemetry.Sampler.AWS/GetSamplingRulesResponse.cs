// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Sampler.AWS;

internal class GetSamplingRulesResponse
{
    [JsonPropertyName("NextToken")]
    public string? NextToken { get; set; }

    [JsonPropertyName("SamplingRuleRecords")]
    public List<SamplingRuleRecord>? SamplingRuleRecords { get; set; }
}
