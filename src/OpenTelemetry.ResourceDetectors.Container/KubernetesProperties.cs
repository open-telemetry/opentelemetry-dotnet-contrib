// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace OpenTelemetry.ResourceDetectors.Container;

internal static class KubernetesProperties
{
    public const string KUBERNETES_PORT_ENV_VAR = "KUBERNETES_PORT";
    public const string KUBERNETES_SERVICE_HOST_ENV_VAR = "KUBERNETES_SERVICE_HOST";
    public const string KUBERNETES_SERVICE_PORT_ENV_VAR = "KUBERNETES_SERVICE_PORT";

    public const string HOSTNAME_ENV_VAR = "HOSTNAME";

    public const string APPDYNAMICS_CONTAINER_NAME_ENV_VAR = "APPDYNAMICS_CONTAINER_NAME";
    public const string APPDYNAMICS_CONTAINER_NAME_SYS_PROP = "appdynamics.container.name";

    public const string POD_NAMESPACE_ENV_VAR = "NAMESPACE";

    public const string Kube_ServiceAcct_Dir_Path = "/var/run/secrets/kubernetes.io/serviceaccount";
    public const string Kube_Api_Cert_File = "ca.crt";
    public const string Kube_Api_Token_File = "token";
    public const string Kube_Api_Namespace_File = "namespace";

    public const string APPDYNAMICS_CONTAINERINFO_FETCH_SERVICE_ENV_VAR = "APPDYNAMICS_CONTAINERINFO_FETCH_SERVICE";
    public const string APPDYNAMICS_CONTAINERINFO_FETCH_SERVICE_SYS_PROP = "appdynamics.containerinfo.fetch.service";

    public const string DISABLE_KUBERNETES_RESOLVER_SYSTEM_PROPERTY = "disable.kubernetes.host.resolver";
    public const string APPD_KUBERNETES_ENV_VAR = "APPDYNAMICS_AGENT_KUBERNETES";

    // Classes exist for Newtonsoft Deserializing
    public class Pod
    {
        public PodStatus status;
    }

    public class PodStatus
    {
        public List<ContainerStatus> containerStatuses;
    }

    public class ContainerStatus
    {
        public string name;
        public string containerID;
    }
}
