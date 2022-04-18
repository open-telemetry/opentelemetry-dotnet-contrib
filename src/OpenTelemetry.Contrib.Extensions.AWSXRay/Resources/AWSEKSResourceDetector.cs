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

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;

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
        var credentials = this.GetEKSCredentials(AWSEKSCredentialPath);
        if (credentials == null || !this.IsEKSProcess(credentials))
        {
            return null;
        }

        return this.ExtractResourceAttributes(
            this.GetEKSClusterName(credentials),
            this.GetEKSContainerId(AWSEKSMetadataFilePath));
    }

    internal List<KeyValuePair<string, object>> ExtractResourceAttributes(string clusterName, string containerId)
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudProvider, "aws"),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudPlatform, "aws_eks"),
        };

        if (!string.IsNullOrEmpty(clusterName))
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeK8SClusterName, clusterName));
        }

        if (!string.IsNullOrEmpty(containerId))
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeContainerID, containerId));
        }

        return resourceAttributes;
    }

    internal string GetEKSCredentials(string path)
    {
        try
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
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSResourceDetector)} : Failed to load client token", ex);
        }

        return null;
    }

    internal string GetEKSContainerId(string path)
    {
        try
        {
            using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
            {
                while (!streamReader.EndOfStream)
                {
                    var trimmedLine = streamReader.ReadLine().Trim();
                    if (trimmedLine.Length > 64)
                    {
                        return trimmedLine.Substring(trimmedLine.Length - 64);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSResourceDetector)} : Failed to get Container Id", ex);
        }

        return null;
    }

    internal AWSEKSClusterInformationModel DeserializeResponse(string response)
    {
        return ResourceDetectorUtils.DeserializeFromString<AWSEKSClusterInformationModel>(response);
    }

    private string GetEKSClusterName(string credentials)
    {
        try
        {
            var clusterInfo = this.GetEKSClusterInfo(credentials);
            return this.DeserializeResponse(clusterInfo)?.Data?.ClusterName;
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSResourceDetector)} : Failed to get cluster information", ex);
        }

        return null;
    }

    private bool IsEKSProcess(string credentials)
    {
        string awsAuth = null;
        try
        {
            var httpClientHandler = this.CreateHttpClientHandler();
            awsAuth = ResourceDetectorUtils.SendOutRequest(AWSAuthUrl, "GET", new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler).Result;
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSResourceDetector)} : Failed to get EKS information", ex);
        }

        return !string.IsNullOrEmpty(awsAuth);
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
