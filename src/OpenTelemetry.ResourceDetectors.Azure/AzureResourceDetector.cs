// <copyright file="AzureResourceDetector.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Resources;

/// <summary>
/// Resource Detector for Azure Environment Variables.
/// </summary>
public class AzureResourceDetector : IResourceDetector
{
    private readonly List<Tuple<string, string>> envVarsToAdd = new()
    {
        new("az.appservice.site_name", "WEBSITE_SITE_NAME"),
        new("az.resource_group", "WEBSITE_RESOURCE_GROUP"),
        new("az.subscription_id", "WEBSITE_OWNER_NAME"),
        new("az.region", "REGION_NAME"),
        new("az.appservice.platform_version", "WEBSITE_PLATFORM_VERSION"),
        new("az.appservice.sku", "WEBSITE_SKU"),
        new("az.appservice.bitness", "SITE_BITNESS"), // x86 vs AMD64
        new("az.appservice.hostname", "WEBSITE_HOSTNAME"),
        new("az.appservice.role_instance_id", "WEBSITE_ROLE_INSTANCE_ID"),
        new("az.appservice.slot_name", "WEBSITE_SLOT_NAME"),
        new("az.appservice.instance_id", "WEBSITE_INSTANCE_ID"),
        new("az.appservice.website_logging_enabled", "WEBSITE_HTTPLOGGING_ENABLED"),
        new("az.appservice.internal_ip", "WEBSITE_PRIVATE_IP"),
        new("az.appservice.functions_extensions_version", "FUNCTIONS_EXTENSION_VERSION"),
        new("az.appservice.functions.worker_runtime", "FUNCTIONS_WORKER_RUNTIME"),
        new("az.appservice.function_placeholder_mode", "WEBSITE_PLACEHOLDER_MODE"),
    };

    /// <summary>
    /// Should the name of the Website (In AppService) be the OpenTelemetry Service Name
    /// </summary>
    public bool UseSiteNameAsServiceName { get; set; } = false;

    /// <inheritdoc/>
    public Resource Detect()
    {
        var resource = ResourceBuilder.CreateDefault();
        var envVars = Environment.GetEnvironmentVariables();

        resource.AddAttributes(
            this.envVarsToAdd
                .Where(attr => envVars.Contains(attr.Item2) &&
                       !string.IsNullOrEmpty(envVars[attr.Item2]?.ToString()))
                .Select(attr =>
                {
                    var (name, key) = attr;
                    return new KeyValuePair<string, object>(name, envVars[key]?.ToString()!);
                }));

        if (this.UseSiteNameAsServiceName &&
            envVars.Contains("WEBSITE_SITE_NAME"))
        {
            resource.AddService(
                envVars["WEBSITE_SITE_NAME"].ToString(),
                autoGenerateServiceInstanceId: false,
                serviceInstanceId:
                    envVars["WEBSITE_ROLE_INSTANCE_ID"]?.ToString() ??
                    envVars["WEBSITE_INSTANCE_ID"]?.ToString());
        }

        return resource.Build();
    }
}
