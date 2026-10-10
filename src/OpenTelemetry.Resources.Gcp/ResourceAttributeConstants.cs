// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Gcp;

internal sealed class ResourceAttributeConstants
{
    // GCP resource attributes constant values
    internal const string GcpCloudProviderValue = "gcp";
    internal const string GcpGcePlatformValue = "gcp_compute_engine";
    internal const string GcpGaePlatformValue = "gcp_app_engine";
    internal const string GcpCloudRunPlatformValue = "gcp_cloud_run";
    internal const string GcpGkePlatformValue = "gcp_kubernetes_engine";

    // GCP-specific resource attribute names
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.44.0/docs/resource/cloud-provider/gcp/cloud-run.md
    internal const string AttributeGcpCloudRunJobExecution = "gcp.cloud_run.job.execution";
    internal const string AttributeGcpCloudRunJobTaskIndex = "gcp.cloud_run.job.task_index";

    // Environment variables set by Cloud Run for job executions
    // https://cloud.google.com/run/docs/container-contract#jobs-env-vars
    internal const string CloudRunExecutionEnvVar = "CLOUD_RUN_EXECUTION";
    internal const string CloudRunTaskIndexEnvVar = "CLOUD_RUN_TASK_INDEX";
}
