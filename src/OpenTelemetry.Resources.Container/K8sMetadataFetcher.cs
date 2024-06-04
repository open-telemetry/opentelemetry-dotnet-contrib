// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;

namespace OpenTelemetry.Resources.Container;

internal sealed class K8sMetadataFetcher : IK8sMetadataFetcher
{
    private const string KubernetesServiceHostKey = "KUBERNETES_SERVICE_HOST";
    private const string KubernetesServicePortKey = "KUBERNETES_SERVICE_PORT_HTTPS";
    private const string KubernetesHostnameKey = "HOSTNAME";
    private const string KubernetesPodNameKey = "KUBERNETES_POD_NAME";
    private const string KubernetesContainerNameKey = "KUBERNETES_CONTAINER_NAME";
    private const string KubernetesNamespacePath = "/var/run/secrets/kubernetes.io/serviceaccount/namespace";
    private const string KubernetesCredentialPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";

    public string? GetApiCredential()
    {
        try
        {
            var stringBuilder = new StringBuilder("Bearer ");

            using (var streamReader = ResourceDetectorUtils.GetStreamReader(KubernetesCredentialPath))
            {
                while (!streamReader.EndOfStream)
                {
                    _ = stringBuilder.Append(streamReader.ReadLine()?.Trim());
                }
            }

            return stringBuilder.ToString();
        }
        catch (Exception ex)
        {
            ContainerResourceEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerDetector)}: Failed to load client token", ex);
        }

        return null;
    }

    public string? GetContainerName()
    {
        return Environment.GetEnvironmentVariable(KubernetesContainerNameKey);
    }

    public string? GetHostname()
    {
        return Environment.GetEnvironmentVariable(KubernetesHostnameKey);
    }

    public string? GetPodName()
    {
        return Environment.GetEnvironmentVariable(KubernetesPodNameKey);
    }

    public string? GetNamespace()
    {
        return File.ReadAllText(KubernetesNamespacePath);
    }

    public string? GetServiceBaseUrl()
    {
        var serviceHost = Environment.GetEnvironmentVariable(KubernetesServiceHostKey);
        var servicePort = Environment.GetEnvironmentVariable(KubernetesServicePortKey);

        if (string.IsNullOrWhiteSpace(serviceHost) || string.IsNullOrWhiteSpace(servicePort))
        {
            return null;
        }

        return $"https://{serviceHost}:{servicePort}";
    }
}
