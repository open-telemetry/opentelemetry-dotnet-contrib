// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.ResourceDetectors.AWS.Models;

internal class AWSEKSClusterDataModel
{
    [JsonPropertyName("cluster.name")]
    public string? ClusterName { get; set; }
}
