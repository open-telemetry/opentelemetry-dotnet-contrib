// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.ResourceDetectors.AWS.Models;

internal sealed class AWSEKSClusterInformationModel
{
    [JsonPropertyName("data")]
    public AWSEKSClusterDataModel? Data { get; set; }
}
