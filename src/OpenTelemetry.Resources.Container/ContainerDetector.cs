// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using OpenTelemetry.Resources.Container.Utils;

namespace OpenTelemetry.Resources.Container;

/// <summary>
/// Resource detector for application running in Container environment.
/// </summary>
internal sealed class ContainerDetector : IResourceDetector
{
    private const string Filepath = "/proc/self/cgroup";
    private const string FilepathV2 = "/proc/self/mountinfo";
    private const string Hostname = "hostname";
    private const string K8sCertificatePath = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";

    private readonly IK8sMetadataFetcher k8sMetadataFetcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerDetector"/> class.
    /// </summary>
    public ContainerDetector()
        : this(new K8sMetadataFetcher())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerDetector"/> class for testing.
    /// </summary>
    /// <param name="k8sMetadataFetcher">The <see cref="IK8sMetadataFetcher"/>.</param>
    internal ContainerDetector(IK8sMetadataFetcher k8sMetadataFetcher)
    {
        this.k8sMetadataFetcher = k8sMetadataFetcher;
    }

    /// <summary>
    /// Detects the resource attributes from Container.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var containerId = this.ExtractK8sContainerId();
        if (!string.IsNullOrEmpty(containerId))
        {
            return BuildResource(containerId);
        }

        containerId = this.ExtractContainerId(Filepath, ParseMode.V1);
        if (!string.IsNullOrEmpty(containerId))
        {
            return BuildResource(containerId);
        }

        containerId = this.ExtractContainerId(FilepathV2, ParseMode.V2);
        if (!string.IsNullOrEmpty(containerId))
        {
            return BuildResource(containerId);
        }

        return Resource.Empty;

        static Resource BuildResource(string containerId)
        {
            return new Resource(new List<KeyValuePair<string, object>>(1) { new(ContainerSemanticConventions.AttributeContainerId, containerId!) });
        }
    }

    /// <summary>
    /// Extracts Container Id from path using the cgroupv1 format.
    /// </summary>
    /// <param name="path">cgroup path.</param>
    /// <param name="parseMode">CGroup Version of file to parse from.</param>
    /// <returns>Container Id, <see langword="null" /> if not found or exception being thrown.</returns>
    internal string? ExtractContainerId(string path, ParseMode parseMode)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            foreach (string line in File.ReadLines(path))
            {
                string? containerId = null;
                if (!string.IsNullOrEmpty(line))
                {
                    if (parseMode == ParseMode.V1)
                    {
                        containerId = GetIdFromLineV1(line);
                    }
                    else if (parseMode == ParseMode.V2 && line.Contains(Hostname, StringComparison.Ordinal))
                    {
                        containerId = GetIdFromLineV2(line);
                    }
                }

                if (!string.IsNullOrEmpty(containerId))
                {
                    return containerId;
                }
            }
        }
        catch (Exception ex)
        {
            ContainerResourceEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerDetector)} : Failed to extract Container id from path", ex);
        }

        return null;
    }

    /// <summary>
    /// Gets the Container Id from the line after removing the prefix and suffix from the cgroupv1 format.
    /// </summary>
    /// <param name="line">line read from cgroup file.</param>
    /// <returns>Container Id, Null if not found.</returns>
    private static string? GetIdFromLineV1(string line)
    {
        // This cgroup output line should have the container id in it
        int lastSlashIndex = line.LastIndexOf('/');
        if (lastSlashIndex < 0)
        {
            return null;
        }

        string lastSection = line.Substring(lastSlashIndex + 1);
        int startIndex = lastSection.LastIndexOf('-');
        int endIndex = lastSection.LastIndexOf('.');

        string containerId = RemovePrefixAndSuffixIfNeeded(lastSection, startIndex, endIndex);

        if (string.IsNullOrEmpty(containerId) || !EncodingUtils.IsValidHexString(containerId))
        {
            return null;
        }

        return containerId;
    }

    /// <summary>
    /// Gets the Container Id from the line of the cgroupv2 format.
    /// </summary>
    /// <param name="line">line read from cgroup file.</param>
    /// <returns>Container Id, Null if not found.</returns>
    private static string? GetIdFromLineV2(string line)
    {
        string? containerId = null;
        var match = Regex.Match(line, @".*/.+/([\w+-.]{64})/.*$");
        if (match.Success)
        {
            containerId = match.Groups[1].Value;
        }

        if (string.IsNullOrEmpty(containerId) || !EncodingUtils.IsValidHexString(containerId!))
        {
            return null;
        }

        return containerId;
    }

    private static string RemovePrefixAndSuffixIfNeeded(string input, int startIndex, int endIndex)
    {
        startIndex = (startIndex == -1) ? 0 : startIndex + 1;
        if (endIndex == -1)
        {
            endIndex = input.Length;
        }

        return input.Substring(startIndex, endIndex - startIndex);
    }

    private string? ExtractK8sContainerId()
    {
        try
        {
            var baseUrl = this.k8sMetadataFetcher.GetServiceBaseUrl();
            var containerName = this.k8sMetadataFetcher.GetContainerName();
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(containerName))
            {
                return null;
            }

            var @namespace = this.k8sMetadataFetcher.GetNamespace();
            var hostname = this.k8sMetadataFetcher.GetPodName() ?? this.k8sMetadataFetcher.GetHostname();
            var url = $"{baseUrl}/api/v1/namespaces/{@namespace}/pods/{hostname}";
            var credentials = this.k8sMetadataFetcher.GetApiCredential();
            if (string.IsNullOrEmpty(credentials))
            {
                return null;
            }

            using var httpClientHandler = ServerCertificateValidationHandler.Create(K8sCertificatePath, ContainerResourceEventSource.Log);
            var response = ResourceDetectorUtils.SendOutRequest(url, "GET", new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler).GetAwaiter().GetResult();
            var pod = ResourceDetectorUtils.DeserializeFromString(response, SourceGenerationContext.Default.K8sPod);
            if (pod?.Status?.ContainerStatuses == null)
            {
                return null;
            }

            var container = pod.Status.ContainerStatuses.SingleOrDefault(p => p.Name == containerName);
            if (string.IsNullOrEmpty(container?.Id))
            {
                return null;
            }

            // Container's ID is in <type>://<container_id> format.
            var index = container.Id.LastIndexOf('/');
            return container.Id.Substring(index + 1);
        }
        catch (Exception ex)
        {
            ContainerResourceEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerDetector)}: Failed to extract container id", ex);
        }

        return null;
    }
}
