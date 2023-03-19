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
    /// <inheritdoc/>
    public Resource Detect()
    {
        List<KeyValuePair<string, object>>? attributeList = null;

        try
        {
            string? serviceName = null;
            string? serviceInstanceId = null;

            // https://learn.microsoft.com/azure/app-service/reference-app-settings?tabs=kudu%2Cdotnet#app-environment
            serviceName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            serviceInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            if (serviceName != null)
            {
                attributeList = new();

                attributeList.Add(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, serviceName));

                if (serviceInstanceId != null)
                {
                    attributeList.Add(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceInstance, serviceInstanceId));
                }
            }
            else
            {
                return Resource.Empty;
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
