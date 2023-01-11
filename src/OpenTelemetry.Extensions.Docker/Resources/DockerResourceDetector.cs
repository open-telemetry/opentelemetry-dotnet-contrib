// <copyright file="DockerResourceDetector.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using OpenTelemetry.Extensions.Docker.Utils;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Extensions.Docker.Resources;

/// <summary>
/// Resource detector for application running in Docker environment.
/// </summary>
public class DockerResourceDetector : IResourceDetector
{
    private const string FILEPATH = "/proc/self/cgroup";
    private const string FILEPATHV2 = "/proc/self/mountinfo";
    private const string HOSTNAME = "hostname";

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
    }

    /// <summary>
    /// Detects the resource attributes from Docker.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var cGroupBuild = this.BuildResource(FILEPATH, ParseMode.V1);
        if (cGroupBuild == Resource.Empty)
        {
            cGroupBuild = this.BuildResource(FILEPATHV2, ParseMode.V2);
        }

        return cGroupBuild;
    }

    /// <summary>
    /// Builds the resource attributes from Container Id in file path.
    /// </summary>
    /// <param name="path">File path where container id exists.</param>
    /// <param name="cgroupVersion">CGroup Version of file to parse from.</param>
    /// <returns>Returns Resource with list of key-value pairs of container resource attributes if container id exists else empty resource.</returns>
    internal Resource BuildResource(string path, ParseMode cgroupVersion)
    {
        var containerId = this.ExtractContainerId(path, cgroupVersion);

        if (string.IsNullOrEmpty(containerId))
        {
            return Resource.Empty;
        }
        else
        {
            return new Resource(new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>(DockerSemanticConventions.AttributeContainerID, containerId), });
        }
    }

    /// <summary>
    /// Extracts Container Id from path using the cgroupv1 format.
    /// </summary>
    /// <param name="path">cgroup path.</param>
    /// <param name="cgroupVersion">CGroup Version of file to parse from.</param>
    /// <returns>Container Id, Null if not found or exception being thrown.</returns>
    private string ExtractContainerId(string path, ParseMode cgroupVersion)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            foreach (string line in File.ReadLines(path))
            {
                string containerId = null;
                if (!string.IsNullOrEmpty(line))
                {
                    if (cgroupVersion == ParseMode.V1)
                    {
                        containerId = this.GetIdFromLineV1(line);
                    }
                    else if (cgroupVersion == ParseMode.V2 && line.Contains(HOSTNAME))
                    {
                        containerId = this.GetIdFromLineV2(line);
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
            DockerExtensionsEventSource.Log.ExtractResourceAttributesException($"{nameof(DockerResourceDetector)} : Failed to extract Container id from path", ex);
        }

        return null;
    }

    /// <summary>
    /// Gets the Container Id from the line after removing the prefix and suffix from the cgroupv1 format.
    /// </summary>
    /// <param name="line">line read from cgroup file.</param>
    /// <returns>Container Id, Null if not found.</returns>
    private string GetIdFromLineV1(string line)
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

        string containerId = this.RemovePrefixAndSuffixIfneeded(lastSection, startIndex, endIndex);

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
    private string GetIdFromLineV2(string line)
    {
        string containerId = null;
        var match = Regex.Match(line, @".*/.+/([\w+-.]{64})/.*$");
        if (match.Success)
        {
            containerId = match.Groups[1].Value;
        }

        if (string.IsNullOrEmpty(containerId) || !EncodingUtils.IsValidHexString(containerId))
        {
            return null;
        }

        return containerId;
    }

    private string RemovePrefixAndSuffixIfneeded(string input, int startIndex, int endIndex)
    {
        startIndex = (startIndex == -1) ? 0 : startIndex + 1;

        if (endIndex == -1)
        {
            endIndex = input.Length;
        }

        return input.Substring(startIndex, endIndex - startIndex);
    }
}
