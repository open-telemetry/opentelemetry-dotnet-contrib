// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class GcpAttributes
{
    /// <summary>
    /// The name of the Cloud Run <a href="https://cloud.google.com/run/docs/managing/job-executions">execution</a> being run for the Job, as set by the <a href="https://cloud.google.com/run/docs/container-contract#jobs-env-vars"><c>CLOUD_RUN_EXECUTION</c></a> environment variable
    /// </summary>
    public const string AttributeGcpCloudRunJobExecution = "gcp.cloud_run.job.execution";

    /// <summary>
    /// The index for a task within an execution as provided by the <a href="https://cloud.google.com/run/docs/container-contract#jobs-env-vars"><c>CLOUD_RUN_TASK_INDEX</c></a> environment variable
    /// </summary>
    public const string AttributeGcpCloudRunJobTaskIndex = "gcp.cloud_run.job.task_index";

    /// <summary>
    /// The hostname of a GCE instance. This is the full value of the default or <a href="https://cloud.google.com/compute/docs/instances/custom-hostname-vm">custom hostname</a>
    /// </summary>
    public const string AttributeGcpGceInstanceHostname = "gcp.gce.instance.hostname";

    /// <summary>
    /// The instance name of a GCE instance. This is the value provided by <c>host.name</c>, the visible name of the instance in the Cloud Console UI, and the prefix for the default hostname of the instance as defined by the <a href="https://cloud.google.com/compute/docs/internal-dns#instance-fully-qualified-domain-names">default internal DNS name</a>
    /// </summary>
    public const string AttributeGcpGceInstanceName = "gcp.gce.instance.name";
}
