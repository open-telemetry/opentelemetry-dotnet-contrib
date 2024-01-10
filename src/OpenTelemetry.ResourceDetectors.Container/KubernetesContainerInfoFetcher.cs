// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Newtonsoft.Json;

namespace OpenTelemetry.ResourceDetectors.Container;

internal class KubernetesContainerInfoFetcher : ContainerInfoFetcher
{
    private readonly string _containerName;

    private KubernetesContainerInfoFetcher(ApiConnector apiConnector, string containerName)
        : base(apiConnector)
    {
        _containerName = containerName;
    }

    // sample response from kube api given BELOW, which needs to be parsed (ignore whitespace in below sample as real data will not have whitespaces)
    protected override string ParseResponse(string response)
    {
        // Following https://kubernetes.io/docs/reference/kubernetes-api/workload-resources/pod-v1/#PodStatus
        KubernetesProperties.Pod? obj = JsonConvert.DeserializeObject<KubernetesProperties.Pod>(response);
        if (obj == null)
        {
            return string.Empty;
        }

        foreach (KubernetesProperties.ContainerStatus containerStatus in obj.status.containerStatuses)
        {
            if (containerStatus.name == this._containerName)
            {
                return this.FormatContainerId(containerStatus.containerID);
            }
        }

        return string.Empty;
    }

    internal static KubernetesContainerInfoFetcher? getInstance()
    {
        bool isRequirementsPresent = CheckAndInitRequirements(out var apiConnector, out var containerName);

        if (isRequirementsPresent)
        {
            return new KubernetesContainerInfoFetcher(apiConnector, containerName);
        }

        return null;
    }

    private class Properties
    {
        public string KubernetesHost;
        public string KubernetesPort;
        public string HostName;
        public string Namespace;
        public string Token;
        public string CertificatePath;
        public string ContainerName;
    }

    private static bool CheckAndInitRequirements(out ApiConnector apiConnector, out string containerName)
    {
        apiConnector = null;
        containerName = null;

        try
        {
            Properties p = new();

            bool requirementsPresent =
                // First check for kube-api-host, kube-api-port, hostname, container-name
                CheckAndInitProp(KubernetesProperties.KUBERNETES_SERVICE_HOST_ENV_VAR, null, out p.KubernetesHost)
                && CheckAndInitProp(KubernetesProperties.KUBERNETES_SERVICE_PORT_ENV_VAR, null, out p.KubernetesPort)
                && CheckAndInitProp(KubernetesProperties.HOSTNAME_ENV_VAR, null, out p.HostName)
                && CheckAndInitProp(
                    KubernetesProperties.APPDYNAMICS_CONTAINER_NAME_ENV_VAR,
                    KubernetesProperties.APPDYNAMICS_CONTAINER_NAME_SYS_PROP,
                    out p.ContainerName)

                // namespace can be extracted as env or from k8 secret file. More preference to env var. (need to change?)
                // check for namespace (env var or file), token file, ca.crt file
                && (CheckAndInitProp(KubernetesProperties.POD_NAMESPACE_ENV_VAR, null, out p.Namespace, true) ||
                    CheckFileAndInitProp(
                        KubernetesProperties.Kube_ServiceAcct_Dir_Path,
                        KubernetesProperties.Kube_Api_Namespace_File,
                        false,
                        out p.Namespace))
                && CheckFileAndInitProp(
                    KubernetesProperties.Kube_ServiceAcct_Dir_Path,
                    KubernetesProperties.Kube_Api_Token_File,
                    false,
                    out p.Token)
                && CheckFileAndInitProp(
                    KubernetesProperties.Kube_ServiceAcct_Dir_Path,
                    KubernetesProperties.Kube_Api_Cert_File,
                    true,
                    out p.CertificatePath);

            if (requirementsPresent)
            {
                apiConnector = new KubeApiConnector(p.KubernetesHost, p.KubernetesPort, p.CertificatePath, p.Token,
                    p.Namespace, p.HostName);
                containerName = p.ContainerName;
                return true;
            }
        }
        catch (Exception)
        {
        }

        return false;
    }
}
