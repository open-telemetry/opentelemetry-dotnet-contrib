// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Resources.Container.Models;

internal sealed class K8sPod
{
    [JsonPropertyName("status")]
    public K8sPodStatus? Status { get; set; }
}
