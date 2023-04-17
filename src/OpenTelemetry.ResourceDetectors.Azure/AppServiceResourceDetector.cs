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
using System.Collections.ObjectModel;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.ResourceDetectors.Azure;

/// <summary>
/// Resource detector for Azure AppService environment.
/// </summary>
public sealed class AppServiceResourceDetector : IResourceDetector
{
    internal static IReadOnlyDictionary<string, string> AppServiceResourceAttributes = new Dictionary<string, string>
    {
        ["appSrv_SiteName"] = "WEBSITE_SITE_NAME",
        [ResourceSemanticConventions.AttributeServiceName] = "WEBSITE_SITE_NAME",
        [ResourceSemanticConventions.AttributeServiceInstance] = "WEBSITE_INSTANCE_ID",
        ["appSrv_SlotName"] = "WEBSITE_SLOT_NAME",
        ["appSrv_wsStamp"] = "WEBSITE_HOME_STAMPNAME",
        ["appSrv_wsHost"] = "WEBSITE_HOSTNAME",
        ["appSrv_wsOwner"] = "WEBSITE_OWNER_NAME",
        ["appSrv_ResourceGroup"] = "WEBSITE_RESOURCE_GROUP",
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
