// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.ResourceDetectors.Container;

internal static class KubernetesProperties
{
    public const string KubernetesPortEnvVar = "KUBERNETES_PORT";
    public const string KubernetesServiceHostEnvVar = "KUBERNETES_SERVICE_HOST";
    public const string KubernetesServicePortEnvVar = "KUBERNETES_SERVICE_PORT";

    public const string HostnameEnvVar = "HOSTNAME";

    public const string ContainerNameEnvVar = "CONTAINER_NAME";
    public const string ContainerNameEnvVar2 = "container.name";

    public const string PodNamespaceEnvVar = "NAMESPACE";

    public const string KubeServiceAcctDirPath = "/var/run/secrets/kubernetes.io/serviceaccount";
    public const string KubeApiCertFile = "ca.crt";
    public const string KubeApiTokenFile = "token";
    public const string KubeApiNamespaceFile = "namespace";

    // Classes exist for Newtonsoft Deserializing
    public class Pod
    {
        [JsonPropertyName("status")]
        public PodStatus? Status { get; set; }
    }

    public class PodStatus
    {
        [JsonPropertyName("containerStatuses")]
        public List<ContainerStatus>? ContainerStatuses { get; set; }
    }

    public class ContainerStatus
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("containerID")]
        public string? ContainerID { get; set; }
    }
}
