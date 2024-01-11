// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.Json;

namespace OpenTelemetry.ResourceDetectors.Container;

internal class KubernetesContainerInfoFetcher : ContainerInfoFetcher
{
    private readonly string containerName;

    private KubernetesContainerInfoFetcher(ApiConnector? apiConnector, string containerName)
        : base(apiConnector)
    {
        this.containerName = containerName;
    }

    internal static KubernetesContainerInfoFetcher? GetInstance()
    {
        bool isRequirementsPresent = CheckAndInitRequirements(out var apiConnector, out var containerName);

        if (isRequirementsPresent && containerName != null)
        {
            return new KubernetesContainerInfoFetcher(apiConnector, containerName);
        }

        if (apiConnector != null)
        {
            apiConnector.Dispose();
        }

        return null;
    }

    // sample response from kube api given BELOW, which needs to be parsed (ignore whitespace in below sample as real data will not have whitespaces)
    protected override string ParseResponse(string response)
    {
        // Following https://kubernetes.io/docs/reference/kubernetes-api/workload-resources/pod-v1/#PodStatus
        var obj = JsonSerializer.Deserialize<KubernetesProperties.Pod>(response);
        if (obj == null || obj.Status == null || obj.Status.ContainerStatuses == null)
        {
            return string.Empty;
        }

        foreach (KubernetesProperties.ContainerStatus containerStatus in obj.Status.ContainerStatuses)
        {
            if (containerStatus.Name == this.containerName)
            {
                if (containerStatus.ContainerID != null)
                {
                    return FormatContainerId(containerStatus.ContainerID);
                }
                else
                {
                    // If the api has not updated the status before this check, the container id will fail to extract
                    return string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static bool CheckAndInitRequirements(out ApiConnector? apiConnector, out string? containerName)
    {
        apiConnector = null;
        containerName = null;

        try
        {
            Properties p = new();

            bool requirementsPresent =

                // First check for kube-api-host, kube-api-port, hostname, container-name
                CheckAndInitProp(KubernetesProperties.KubernetesServiceHostEnvVar, null, out p.KubernetesHost)
                && CheckAndInitProp(KubernetesProperties.KubernetesServicePortEnvVar, null, out p.KubernetesPort)
                && CheckAndInitProp(KubernetesProperties.HostnameEnvVar, null, out p.HostName)
                && CheckAndInitProp(
                    KubernetesProperties.ContainerNameEnvVar,
                    KubernetesProperties.ContainerNameSysProp,
                    out p.ContainerName)

                // namespace can be extracted as env or from k8 secret file. More preference to env var. (need to change?)
                // check for namespace (env var or file), token file, ca.crt file
                && (CheckAndInitProp(KubernetesProperties.PodNamespaceEnvVar, null, out p.Namespace, true) ||
                    CheckFileAndInitProp(
                        KubernetesProperties.KubeServiceAcctDirPath,
                        KubernetesProperties.KubeApiNamespaceFile,
                        false,
                        out p.Namespace))
                && CheckFileAndInitProp(
                    KubernetesProperties.KubeServiceAcctDirPath,
                    KubernetesProperties.KubeApiTokenFile,
                    false,
                    out p.Token)
                && CheckFileAndInitProp(
                    KubernetesProperties.KubeServiceAcctDirPath,
                    KubernetesProperties.KubeApiCertFile,
                    true,
                    out p.CertificatePath);

            if (requirementsPresent)
            {
                apiConnector = new KubeApiConnector(p.KubernetesHost, p.KubernetesPort, p.CertificatePath, p.Token, p.Namespace, p.HostName);
                containerName = p.ContainerName;
                return true;
            }
        }
        catch (Exception)
        {
        }

        return false;
    }

    private class Properties
    {
        public string? KubernetesHost;
        public string? KubernetesPort;
        public string? HostName;
        public string? Namespace;
        public string? Token;
        public string? CertificatePath;
        public string? ContainerName;
    }
}
