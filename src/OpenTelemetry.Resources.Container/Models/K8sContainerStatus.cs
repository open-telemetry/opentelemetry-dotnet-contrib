// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Resources.Container.Models;

internal sealed class K8sContainerStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("containerID")]
    public string Id { get; set; } = default!;
}
