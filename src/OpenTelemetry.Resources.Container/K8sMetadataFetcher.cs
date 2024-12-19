// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Resources.Container;

internal sealed class K8sMetadataFetcher : IK8sMetadataFetcher
{
    private const string KubernetesServiceHostEnvVar = "KUBERNETES_SERVICE_HOST";
    private const string KubernetesServicePortEnvVar = "KUBERNETES_SERVICE_PORT_HTTPS";
    private const string KubernetesHostnameEnvVar = "HOSTNAME";
    private const string KubernetesPodNameEnvVar = "KUBERNETES_POD_NAME";
    private const string KubernetesContainerNameEnvVar = "KUBERNETES_CONTAINER_NAME";
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
        return Environment.GetEnvironmentVariable(KubernetesContainerNameEnvVar);
    }

    public string? GetHostname()
    {
        return Environment.GetEnvironmentVariable(KubernetesHostnameEnvVar);
    }

    public string? GetPodName()
    {
        return Environment.GetEnvironmentVariable(KubernetesPodNameEnvVar);
    }

    public string? GetNamespace()
    {
        return File.ReadAllText(KubernetesNamespacePath);
    }

    public string? GetServiceBaseUrl()
    {
        var serviceHost = Environment.GetEnvironmentVariable(KubernetesServiceHostEnvVar);
        var servicePort = Environment.GetEnvironmentVariable(KubernetesServicePortEnvVar);

        if (string.IsNullOrWhiteSpace(serviceHost) || string.IsNullOrWhiteSpace(servicePort))
        {
            return null;
        }

        return $"https://{serviceHost}:{servicePort}";
    }
}
