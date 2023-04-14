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

namespace OpenTelemetry.ResourceDetectors.Azure;

/// <summary>
/// Resource detector for Azure AppService environment.
/// </summary>
public sealed class AppServiceResourceDetector : IResourceDetector
{
    /// <inheritdoc/>
    public Resource Detect()
    {
        List<KeyValuePair<string, object>> attributeList = new();

        try
        {
            var website_SlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");
            if (website_SlotName != null)
            {
                attributeList.Add(new KeyValuePair<string, object>("appSrv_SiteName", website_SlotName));
            }

            var website_Name = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            if (website_Name != null)
            {
                attributeList.Add(new KeyValuePair<string, object>("appSrv_SiteName", website_Name));
            }

            var website_Home_Stampname = Environment.GetEnvironmentVariable("WEBSITE_HOME_STAMPNAME");
            if (website_Home_Stampname != null)
            {
                attributeList.Add(new KeyValuePair<string, object>("appSrv_wsStamp", website_Home_Stampname));
            }

            var website_HostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            if (website_HostName != null)
            {
                attributeList.Add(new KeyValuePair<string, object>("appSrv_wsHost", website_HostName));
            }

            var website_Owner_Name = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME");
            if (website_Owner_Name != null)
            {
                attributeList.Add(new KeyValuePair<string, object>("appSrv_wsOwner", website_Owner_Name));
            }

            var website_Resource_Group = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            if (website_Resource_Group != null)
            {
                attributeList.Add(new KeyValuePair<string, object>("appSrv_ResourceGroup", website_Resource_Group));
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
