// <copyright file="Constants.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Stackdriver.Implementation;

internal class Constants
{
    public const string PackagVersionUndefined = "undefined";

    public const string LabelDescription = "OpenTelemetry string";
    public const string OpenTelemetryTask = "OpenTelemetry_task";
    public const string OpenTelemetryTaskDescription = "OpenTelemetry task identifier";

    public const string GcpGkeContainer = "k8s_container";
    public const string GcpGceInstance = "gce_instance";
    public const string AwsEc2Instance = "aws_ec2_instance";
    public const string Global = "global";

    public const string ProjectIdLabelKey = "project_id";

    public const string GceGcpInstanceType = "cloud.google.com/gce/instance";
    public const string GcpInstanceIdKey = "cloud.google.com/gce/instance_id";
    public const string GcpAccountIdKey = "cloud.google.com/gce/project_id";
    public const string GcpZoneKey = "cloud.google.com/gce/zone";

    public const string K8sContainerType = "k8s.io/container";
    public const string K8sClusterNameKey = "k8s.io/cluster/name";
    public const string K8sContainerNameKey = "k8s.io/container/name";
    public const string K8sNamespaceNameKey = "k8s.io/namespace/name";
    public const string K8sPodNameKey = "k8s.io/pod/name";

    public static readonly string OpenTelemetryTaskValueDefault = GenerateDefaultTaskValue();

    private static string GenerateDefaultTaskValue()
    {
        // Something like '<pid>@<hostname>'
        return $"dotnet-{System.Diagnostics.Process.GetCurrentProcess().Id}@{Environment.MachineName}";
    }
}
