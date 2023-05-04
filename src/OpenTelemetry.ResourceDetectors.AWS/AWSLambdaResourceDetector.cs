// <copyright file="AWSLambdaResourceDetector.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.AWS;

/// <summary>
/// Resource detector for application running in AWS Lambda.
/// </summary>
public class AWSLambdaResourceDetector : IResourceDetector
{
    private const string AWSLambdaRegion = "AWS_REGION";
    private const string AWSLambdaFunctionName = "AWS_LAMBDA_FUNCTION_NAME";
    private const string AWSLambdaFunctionVersion = "AWS_LAMBDA_FUNCTION_VERSION";

    /// <summary>
    /// Detector the required and optional resource attributes from AWS Lambda.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        try
        {
            return new(ExtractResourceAttributes());
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSLambdaResourceDetector), ex);
        }

        return Resource.Empty;
    }

    internal static List<KeyValuePair<string, object>> ExtractResourceAttributes()
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new(AWSSemanticConventions.AttributeCloudProvider, "aws"),
            new(AWSSemanticConventions.AttributeCloudPlatform, "aws_lambda"),
            new(AWSSemanticConventions.AttributeCloudRegion, GetAWSRegion()),
            new(AWSSemanticConventions.AttributeFaasName, GetFunctionName()),
            new(AWSSemanticConventions.AttributeFaasVersion, GetFunctionVersion()),
        };

        return resourceAttributes;
    }

    private static string GetAWSRegion()
    {
        return Environment.GetEnvironmentVariable(AWSLambdaRegion);
    }

    private static string GetFunctionName()
    {
        return Environment.GetEnvironmentVariable(AWSLambdaFunctionName);
    }

    private static string GetFunctionVersion()
    {
        return Environment.GetEnvironmentVariable(AWSLambdaFunctionVersion);
    }
}
