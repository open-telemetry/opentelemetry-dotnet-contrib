// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
#if !NETFRAMEWORK
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenTelemetry.ResourceDetectors.Container;

internal class KubernetesContainerInfoFetcher
{
    private static readonly Regex HexStringRegex = new("(^[a-fA-F0-9]+$)", RegexOptions.Compiled);

    private readonly string containerName;

    private readonly KubeApiConnector? apiConnector;

    private KubernetesContainerInfoFetcher(KubeApiConnector? apiConnector, string containerName)
    {
        this.apiConnector = apiConnector;
        this.containerName = containerName;
    }

    public string ExtractContainerId()
    {
        // executing request only once. Do we need to retry again if data not available?
        string response = this.ExecuteApiRequest();
        if (response == null)
        {
            return string.Empty;
        }

        return this.ParseResponse(response);
    }

    internal static KubernetesContainerInfoFetcher? GetInstance()
    {
        bool isRequirementsPresent = CheckAndInitRequirements(out var apiConnector, out var containerName);

        if (isRequirementsPresent && containerName != null)
        {
            return new KubernetesContainerInfoFetcher(apiConnector, containerName);
        }

        return null;
    }

    protected static bool CheckAndInitProp(string envPropName1, string? envPropName2, out string? result, bool canContinue = false)
    {
        string? value = Environment.GetEnvironmentVariable(envPropName1);

        if (value == null && envPropName2 != null)
        {
            value = Environment.GetEnvironmentVariable(envPropName2);
        }

        if (value == null || string.IsNullOrEmpty(value))
        {
            result = null;

            return false;
        }

        result = value;
        return true;
    }

    protected static bool CheckFileAndInitProp(string dirName, string fileName, bool isCertFile, out string result)
    {
        result = string.Empty;
        string filePath = Path.Combine(dirName, fileName);
        try
        {
            FileInfo fileInfo = new(filePath);

            if (!IsFilePresent(fileInfo))
            {
                return false;
            }

            // if this is certificate file, we don't have to read it yet but only check for existence and readability
            // ca.cert will be directly consumed as input stream when building SSL context
            if (isCertFile)
            {
                result = Path.GetFullPath(filePath);
            }
            else
            {
                string data = File.ReadAllText(filePath).Trim();

                // file only has whitespaces
                if (string.IsNullOrEmpty(data))
                {
                    return false;
                }

                result = data;
            }
        }
        catch (Exception e)
        {
            ContainerExtensionsEventSource.Log.ExtractResourceAttributesException("Cannot Read " + Path.GetFullPath(filePath) + " : " + e.Message, e);
            return false;
        }

        return true;
    }

    protected static string FormatContainerId(string unFormattedId)
    {
        // "containerID"="docker://18e1f4b72f6861b5e591e11ea6db0640377de6ed5dc9bffbae4d9ab284d53044"
        // Assuming kube api return container id always in this format prefixed with 'docker://'. (Big assumption?)
        string formattedId = unFormattedId.Substring(unFormattedId.LastIndexOf("/", StringComparison.InvariantCulture) + 1);

        // should be valid hex string
        if (!HexStringRegex.Match(formattedId).Success)
        {
            return string.Empty;
        }

        return formattedId;
    }

    protected string ParseResponse(string response)
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

    private static bool IsFilePresent(FileInfo fileInfo)
    {
        return fileInfo.Exists && (fileInfo.Length != 0);
    }

    private static bool CheckAndInitRequirements(out KubeApiConnector? apiConnector, out string? containerName)
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
                    KubernetesProperties.ContainerNameEnvVar2,
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

    private string ExecuteApiRequest()
    {
        return this.apiConnector!.ExecuteRequest();
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
#endif
