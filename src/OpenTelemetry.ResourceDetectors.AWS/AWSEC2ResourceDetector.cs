// <copyright file="AWSEC2ResourceDetector.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.ResourceDetectors.AWS.Models;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.AWS;

/// <summary>
/// Resource detector for application running on AWS EC2 instance.
/// </summary>
public class AWSEC2ResourceDetector : IResourceDetector
{
    private const string AWSEC2MetadataTokenTTLHeader = "X-aws-ec2-metadata-token-ttl-seconds";
    private const string AWSEC2MetadataTokenHeader = "X-aws-ec2-metadata-token";
    private const string AWSEC2MetadataTokenUrl = "http://169.254.169.254/latest/api/token";
    private const string AWSEC2HostNameUrl = "http://169.254.169.254/latest/meta-data/hostname";
    private const string AWSEC2IdentityDocumentUrl = "http://169.254.169.254/latest/dynamic/instance-identity/document";

    /// <summary>
    /// Detector the required and optional resource attributes from AWS EC2.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        try
        {
            var token = GetAWSEC2Token();
            var identity = GetAWSEC2Identity(token);
            var hostName = GetAWSEC2HostName(token);

            return new Resource(ExtractResourceAttributes(identity, hostName));
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSEC2ResourceDetector), ex);
        }

        return Resource.Empty;
    }

    internal static List<KeyValuePair<string, object>> ExtractResourceAttributes(AWSEC2IdentityDocumentModel? identity, string hostName)
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new(AWSSemanticConventions.AttributeCloudProvider, "aws"),
            new(AWSSemanticConventions.AttributeCloudPlatform, "aws_ec2"),
            new(AWSSemanticConventions.AttributeHostName, hostName),
        };

        if (identity != null)
        {
            if (identity.AccountId != null)
            {
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudAccountID, identity.AccountId));
            }

            if (identity.AvailabilityZone != null)
            {
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudAvailableZone, identity.AvailabilityZone));
            }

            if (identity.InstanceId != null)
            {
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeHostID, identity.InstanceId));
            }

            if (identity.InstanceType != null)
            {
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeHostType, identity.InstanceType));
            }

            if (identity.Region != null)
            {
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudRegion, identity.Region));
            }
        }

        return resourceAttributes;
    }

    internal static AWSEC2IdentityDocumentModel? DeserializeResponse(string response)
    {
        return ResourceDetectorUtils.DeserializeFromString<AWSEC2IdentityDocumentModel>(response);
    }

    private static string GetAWSEC2Token()
    {
        return ResourceDetectorUtils.SendOutRequest(AWSEC2MetadataTokenUrl, "PUT", new KeyValuePair<string, string>(AWSEC2MetadataTokenTTLHeader, "60")).Result;
    }

    private static AWSEC2IdentityDocumentModel? GetAWSEC2Identity(string token)
    {
        var identity = GetIdentityResponse(token);
        var identityDocument = DeserializeResponse(identity);

        return identityDocument;
    }

    private static string GetIdentityResponse(string token)
    {
        return ResourceDetectorUtils.SendOutRequest(AWSEC2IdentityDocumentUrl, "GET", new KeyValuePair<string, string>(AWSEC2MetadataTokenHeader, token)).Result;
    }

    private static string GetAWSEC2HostName(string token)
    {
        return ResourceDetectorUtils.SendOutRequest(AWSEC2HostNameUrl, "GET", new KeyValuePair<string, string>(AWSEC2MetadataTokenHeader, token)).Result;
    }
}
