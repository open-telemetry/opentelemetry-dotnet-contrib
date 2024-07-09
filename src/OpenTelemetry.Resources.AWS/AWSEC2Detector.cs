// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Resources.AWS.Models;

namespace OpenTelemetry.Resources.AWS;

/// <summary>
/// Resource detector for application running on AWS EC2 instance.
/// </summary>
internal sealed class AWSEC2Detector : IResourceDetector
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
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSEC2Detector), ex);
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
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudAvailabilityZone, identity.AvailabilityZone));
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
#if NET
        return ResourceDetectorUtils.DeserializeFromString(response, SourceGenerationContext.Default.AWSEC2IdentityDocumentModel);
#else
        return ResourceDetectorUtils.DeserializeFromString<AWSEC2IdentityDocumentModel>(response);
#endif
    }

    private static string GetAWSEC2Token()
    {
        return AsyncHelper.RunSync(() => ResourceDetectorUtils.SendOutRequestAsync(AWSEC2MetadataTokenUrl, HttpMethod.Put, new KeyValuePair<string, string>(AWSEC2MetadataTokenTTLHeader, "60")));
    }

    private static AWSEC2IdentityDocumentModel? GetAWSEC2Identity(string token)
    {
        var identity = GetIdentityResponse(token);
        var identityDocument = DeserializeResponse(identity);

        return identityDocument;
    }

    private static string GetIdentityResponse(string token)
    {
        return AsyncHelper.RunSync(() => ResourceDetectorUtils.SendOutRequestAsync(AWSEC2IdentityDocumentUrl, HttpMethod.Get, new KeyValuePair<string, string>(AWSEC2MetadataTokenHeader, token)));
    }

    private static string GetAWSEC2HostName(string token)
    {
        return AsyncHelper.RunSync(() => ResourceDetectorUtils.SendOutRequestAsync(AWSEC2HostNameUrl, HttpMethod.Get, new KeyValuePair<string, string>(AWSEC2MetadataTokenHeader, token)));
    }
}
