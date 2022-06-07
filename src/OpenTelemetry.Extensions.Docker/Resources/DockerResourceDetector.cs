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
using OpenTelemetry.Extensions.Docker.Utils;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Extensions.Docker.Resources
{
    /// <summary>
    /// Resource detector for application running in Docker environment.
    /// </summary>
    public class DockerResourceDetector : IResourceDetector
    {
        private const string FILEPATH = "/proc/self/cgroup";

        /// <summary>
        /// Detects the resource attributes from Docker.
        /// </summary>
        /// <returns>Resource with key-value pairs of resource attributes.</returns>
        public Resource Detect()
        {
            return this.BuildResource(FILEPATH);
        }

        /// <summary>
        /// Builds the resource attributes from Container Id in file path.
        /// </summary>
        /// <param name="path">File path where container id exists.</param>
        /// <returns>Returns Resource with list of key-value pairs of container resource attributes if container id exists else empty resource.</returns>
        internal Resource BuildResource(string path)
        {
            var containerId = this.ExtractContainerId(path);

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
        /// Extracts Container Id from path.
        /// </summary>
        /// <param name="path">cgroup path.</param>
        /// <returns>Container Id, Null if not found or exception being thrown.</returns>
        private string ExtractContainerId(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                foreach (string line in File.ReadLines(path))
                {
                    string containerId = (!string.IsNullOrEmpty(line)) ? this.GetIdFromLine(line) : null;
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
        /// Gets the Container Id from the line after removing the prefix and suffix.
        /// </summary>
        /// <param name="line">line read from cgroup file.</param>
        /// <returns>Container Id.</returns>
        private string GetIdFromLine(string line)
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
}
