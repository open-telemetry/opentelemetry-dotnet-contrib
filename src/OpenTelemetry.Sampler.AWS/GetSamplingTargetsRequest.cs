// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Sampler.AWS;

internal class GetSamplingTargetsRequest
{
    public GetSamplingTargetsRequest(List<SamplingStatisticsDocument> documents)
    {
        this.SamplingStatisticsDocuments = documents;
    }

    [JsonPropertyName("SamplingStatisticsDocuments")]
    public List<SamplingStatisticsDocument> SamplingStatisticsDocuments { get; set; }
}
