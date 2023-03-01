// <copyright file="AWSEBSMetadataModel.cs" company="OpenTelemetry Authors">
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

using Newtonsoft.Json;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources.Models;

internal class AWSEBSMetadataModel
{
    [JsonProperty(PropertyName = "deployment_id")]
    public string? DeploymentId { get; set; }

    [JsonProperty(PropertyName = "environment_name")]
    public string? EnvironmentName { get; set; }

    [JsonProperty(PropertyName = "version_label")]
    public string? VersionLabel { get; set; }
}
