// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.ResourceDetectors.Container.Models;

internal sealed class K8sPodStatus
{
    [JsonPropertyName("containerStatuses")]
    public IReadOnlyList<K8sContainerStatus> ContainerStatuses { get; set; } = new List<K8sContainerStatus>();
}
