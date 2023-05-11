// <copyright file="AppServiceResourceDetector.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry.ResourceDetectors.Azure;

/// <summary>
/// Resource detector for Azure AppService environment.
/// </summary>
public sealed class AppServiceResourceDetector : IResourceDetector
{
    internal static readonly IReadOnlyDictionary<string, string> AppServiceResourceAttributes = new Dictionary<string, string>
    {
        [ResourceAttributeConstants.AppserviceSiteName] = ResourceAttributeConstants.AppserviceSiteNameEnvVar,
        [ResourceSemanticConventions.AttributeServiceName] = ResourceAttributeConstants.AppserviceSiteNameEnvVar,
        [ResourceSemanticConventions.AttributeServiceInstance] = ResourceAttributeConstants.AppserviceInstanceIdEnvVar,
        [ResourceAttributeConstants.AppserviceSlotName] = ResourceAttributeConstants.AppserviceSlotNameEnvVar,
        [ResourceAttributeConstants.AppserviceWsStamp] = ResourceAttributeConstants.AppserviceStampNameEnvVar,
        [ResourceAttributeConstants.AppserviceWsHost] = ResourceAttributeConstants.AppserviceHostNameEnvVar,
        [ResourceAttributeConstants.AppserviceOwner] = ResourceAttributeConstants.AppserviceOwnerNameEnvVar,
        [ResourceAttributeConstants.AppserviceResourceGroup] = ResourceAttributeConstants.AppserviceResourceGroupEnvVar,
    };

    /// <inheritdoc/>
    public Resource Detect()
    {
        List<KeyValuePair<string, object>> attributeList = new();

        try
        {
            foreach (var kvp in AppServiceResourceAttributes)
            {
                var attributeValue = Environment.GetEnvironmentVariable(kvp.Value);
                if (attributeValue != null)
                {
                    attributeList.Add(new KeyValuePair<string, object>(kvp.Key, attributeValue));
                }
            }
        }
        catch
        {
            // TODO: log exception.
            return Resource.Empty;
        }

        return new Resource(attributeList);
    }
}
