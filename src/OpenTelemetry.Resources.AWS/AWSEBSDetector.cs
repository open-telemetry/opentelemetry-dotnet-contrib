// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Runtime.InteropServices;
#endif
using OpenTelemetry.AWS;
using OpenTelemetry.Resources.AWS.Models;

namespace OpenTelemetry.Resources.AWS;

/// <summary>
/// Resource detector for application running in AWS Elastic Beanstalk environment.
/// </summary>
internal sealed class AWSEBSDetector : IResourceDetector
{
    private const string AWSEBSMetadataWindowsFilePath = "C:\\Program Files\\Amazon\\XRay\\environment.conf";
#if NET
    private const string AWSEBSMetadataLinuxFilePath = "/var/elasticbeanstalk/xray/environment.conf";
#endif

    /// <summary>
    /// Detector the required and optional resource attributes from AWS Elastic Beanstalk.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        try
        {
            string? filePath;
#if NET
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filePath = AWSEBSMetadataWindowsFilePath;
            }
            else
            {
                filePath = AWSEBSMetadataLinuxFilePath;
            }
#else
            filePath = AWSEBSMetadataWindowsFilePath;
#endif

            var metadata = GetEBSMetadata(filePath);

            return new Resource(ExtractResourceAttributes(metadata));
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSEBSDetector), ex);
        }

        return Resource.Empty;
    }

    internal static List<KeyValuePair<string, object>> ExtractResourceAttributes(AWSEBSMetadataModel? metadata)
    {
        var resourceAttributes =
            new List<KeyValuePair<string, object>>()
                .AddAttributeCloudProviderIsAWS()
                .AddAttributeCloudPlatformIsAwsElasticBeanstalk()
                .AddAttributeServiceNameIsAwsElasticBeanstalk()
                .AddAttributeServiceNamespace(metadata?.EnvironmentName)
                .AddAttributeServiceInstanceID(metadata?.DeploymentId)
                .AddAttributeServiceVersion(metadata?.VersionLabel);

        return resourceAttributes;
    }

    internal static AWSEBSMetadataModel? GetEBSMetadata(string filePath)
    {
#if NETFRAMEWORK
        return ResourceDetectorUtils.DeserializeFromFile<AWSEBSMetadataModel>(filePath);
#else
        return ResourceDetectorUtils.DeserializeFromFile(filePath, SourceGenerationContext.Default.AWSEBSMetadataModel);
#endif
    }
}
