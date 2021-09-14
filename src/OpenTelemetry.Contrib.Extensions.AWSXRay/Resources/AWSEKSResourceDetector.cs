// <copyright file="AWSEKSResourceDetector.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources.Models;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources
{
    /// <summary>
    /// Resource detector for application running in AWS EKS.
    /// </summary>
    public class AWSEKSResourceDetector : IResourceDetector
    {
        private const string AWSEKSCertificatePath = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";
        private const string AWSEKSCredentialPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
        private const string AWSEKSMetadataFilePath = "/proc/self/cgroup";
        private const string AWSClusterInfoUrl = "https://kubernetes.default.svc/api/v1/namespaces/amazon-cloudwatch/configmaps/cluster-info";
        private const string AWSAuthUrl = "https://kubernetes.default.svc/api/v1/namespaces/kube-system/configmaps/aws-auth";

        /// <summary>
        /// Detector the required and optional resource attributes from AWS EKS.
        /// </summary>
        /// <returns>List of key-value pairs of resource attributes.</returns>
        public IEnumerable<KeyValuePair<string, object>> Detect()
        {
            List<KeyValuePair<string, object>> resourceAttributes = null;

            try
            {
                var clusterName = this.GetEKSClusterName(AWSEKSCredentialPath);
                var containerId = this.GetEKSContainerId(AWSEKSMetadataFilePath);

                if (clusterName == null && containerId == null)
                {
                    return resourceAttributes;
                }

                resourceAttributes = new List<KeyValuePair<string, object>>()
                {
                    new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudProvider, "aws"),
                    new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudPlatform, "aws_eks"),
                    new KeyValuePair<string, object>(AWSSemanticConventions.AttributeK8SClusterName, clusterName),
                    new KeyValuePair<string, object>(AWSSemanticConventions.AttributeContainerID, containerId),
                };
            }
            catch (Exception ex)
            {
                AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSEKSResourceDetector), ex);
            }

            return resourceAttributes;
        }

        internal string GetEKSCredentials(string path)
        {
            StringBuilder stringBuilder = new StringBuilder();

            using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
            {
                while (!streamReader.EndOfStream)
                {
                    stringBuilder.Append(streamReader.ReadLine().Trim());
                }
            }

            return "Bearer " + stringBuilder.ToString();
        }

        internal string GetEKSContainerId(string path)
        {
            string containerId = null;

            using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
            {
                while (!streamReader.EndOfStream)
                {
                    var trimmedLine = streamReader.ReadLine().Trim();
                    if (trimmedLine.Length > 64)
                    {
                        containerId = trimmedLine.Substring(trimmedLine.Length - 64);
                        return containerId;
                    }
                }
            }

            return containerId;
        }

        internal AWSEKSClusterInformationModel DeserializeResponse(string response)
        {
            return ResourceDetectorUtils.DeserializeFromString<AWSEKSClusterInformationModel>(response);
        }

        private string GetEKSClusterName(string path)
        {
            var credentials = this.GetEKSCredentials(path);

            if (!this.IsEKSProcess(credentials))
            {
                return null;
            }

            var clusterInfo = this.GetEKSClusterInfo(credentials);
            var clusterInfoObject = this.DeserializeResponse(clusterInfo);

            return clusterInfoObject.Data?.ClusterName;
        }

        private bool IsEKSProcess(string credentials)
        {
            var httpClientHandler = this.CreateHttpClientHandler();
            var awsAuth = ResourceDetectorUtils.SendOutRequest(AWSAuthUrl, "GET", new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler).Result;
            return string.IsNullOrEmpty(awsAuth);
        }

        private string GetEKSClusterInfo(string credentials)
        {
            var httpClientHandler = this.CreateHttpClientHandler();
            return ResourceDetectorUtils.SendOutRequest(AWSClusterInfoUrl, "GET", new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler).Result;
        }

        private HttpClientHandler CreateHttpClientHandler()
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ClientCertificates.Add(new X509Certificate2(AWSEKSCertificatePath));
            return httpClientHandler;
        }
    }
}
