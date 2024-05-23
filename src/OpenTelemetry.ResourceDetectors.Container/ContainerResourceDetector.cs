// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenTelemetry.ResourceDetectors.Container.Models;
using OpenTelemetry.ResourceDetectors.Container.Utils;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.Container;

/// <summary>
/// Resource detector for application running in Container environment.
/// </summary>
public class ContainerResourceDetector : IResourceDetector
{
    private const string Filepath = "/proc/self/cgroup";
    private const string FilepathV2 = "/proc/self/mountinfo";
    private const string Hostname = "hostname";
    private const string K8sServiceHostKey = "KUBERNETES_SERVICE_HOST";
    private const string K8sServicePortKey = "KUBERNETES_SERVICE_PORT_HTTPS";
    private const string K8sHostnameKey = "HOSTNAME";
    private const string K8sPodNameKey = "KUBERNETES_POD_NAME";
    private const string K8sContainerNameKey = "KUBERNETES_CONTAINER_NAME";
    private const string K8sNamespacePath = "/var/run/secrets/kubernetes.io/serviceaccount/namespace";
    private const string K8sCertificatePath = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";
    private const string K8sCredentialPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";

    /// <summary>
    /// CGroup Parse Versions.
    /// </summary>
    internal enum ParseMode
    {
        /// <summary>
        /// Represents CGroupV1.
        /// </summary>
        V1,

        /// <summary>
        /// Represents CGroupV2.
        /// </summary>
        V2,

        /// <summary>
        /// Represents Kubernetes.
        /// </summary>
        K8s,
    }

    /// <summary>
    /// Detects the resource attributes from Container.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var resource = this.BuildResource(Filepath, ParseMode.K8s);
        if (resource == Resource.Empty)
        {
            resource = this.BuildResource(Filepath, ParseMode.V1);
        }

        if (resource == Resource.Empty)
        {
            resource = this.BuildResource(FilepathV2, ParseMode.V2);
        }

        return resource;
    }

    /// <summary>
    /// Builds the resource attributes from Container Id in file path.
    /// </summary>
    /// <param name="path">File path where container id exists.</param>
    /// <param name="parseMode">CGroup Version of file to parse from.</param>
    /// <returns>Returns Resource with list of key-value pairs of container resource attributes if container id exists else empty resource.</returns>
    internal Resource BuildResource(string path, ParseMode parseMode)
    {
        var containerId = this.ExtractContainerId(path, parseMode);

        if (string.IsNullOrEmpty(containerId))
        {
            return Resource.Empty;
        }
        else
        {
            return new Resource(new List<KeyValuePair<string, object>>(1) { new(ContainerSemanticConventions.AttributeContainerId, containerId!) });
        }
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

    private static string? ExtractK8sContainerId()
    {
        try
        {
            var host = Environment.GetEnvironmentVariable(K8sServiceHostKey);
            var port = Environment.GetEnvironmentVariable(K8sServicePortKey);
            var @namespace = File.ReadAllText(K8sNamespacePath);
            var hostname = Environment.GetEnvironmentVariable(K8sPodNameKey) ?? Environment.GetEnvironmentVariable(K8sHostnameKey);
            var containerName = Environment.GetEnvironmentVariable(K8sContainerNameKey);
            var url = $"https://{host}:{port}/api/v1/namespaces/{@namespace}/pods/{hostname}";
            var credentials = GetK8sCredentials(K8sCredentialPath);
            if (string.IsNullOrEmpty(credentials))
            {
                return string.Empty;
            }

            using var httpClientHandler = ServerCertificateValidationHandler.Create(K8sCertificatePath, ContainerResourceEventSource.Log);
            var response = ResourceDetectorUtils.SendOutRequest(url, "GET", new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler).GetAwaiter().GetResult();
            var pod = DeserializeK8sResponse(response);
            if (pod?.Status?.ContainerStatuses == null)
            {
                return string.Empty;
            }

            var container = pod.Status.ContainerStatuses.SingleOrDefault(p => p.Name == containerName);
            if (container is null || string.IsNullOrEmpty(container.Id))
            {
                return string.Empty;
            }

            // Container's ID is in <type>://<container_id> format.
            var index = container.Id.LastIndexOf('/');
            return container.Id.Substring(index + 1);
        }
        catch (Exception ex)
        {
            ContainerResourceEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerResourceDetector)}: Failed to extract container id", ex);
        }

        return null;

        static string? GetK8sCredentials(string path)
        {
            try
            {
                var stringBuilder = new StringBuilder("Bearer ");

                using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
                {
                    while (!streamReader.EndOfStream)
                    {
                        stringBuilder.Append(streamReader.ReadLine()?.Trim());
                    }
                }

                return stringBuilder.ToString();
            }
            catch (Exception ex)
            {
                ContainerResourceEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerResourceDetector)}: Failed to load client token", ex);
            }

            return null;
        }

        static K8sPod? DeserializeK8sResponse(string response)
        {
#if NET6_0_OR_GREATER
            return ResourceDetectorUtils.DeserializeFromString(response, SourceGenerationContext.Default.K8sPod);
#else
            return ResourceDetectorUtils.DeserializeFromString<K8sPod>(response);
#endif
        }
    }

    /// <summary>
    /// Extracts Container Id from path using the cgroupv1 format.
    /// </summary>
    /// <param name="path">cgroup path.</param>
    /// <param name="parseMode">CGroup Version of file to parse from.</param>
    /// <returns>Container Id, <see langword="null" /> if not found or exception being thrown.</returns>
    private string? ExtractContainerId(string path, ParseMode parseMode)
    {
        try
        {
            if (parseMode == ParseMode.K8s)
            {
                return ExtractK8sContainerId();
            }
            else
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
        }
        catch (Exception ex)
        {
            ContainerResourceEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerResourceDetector)} : Failed to extract Container id from path", ex);
        }

        return null;
    }
}
