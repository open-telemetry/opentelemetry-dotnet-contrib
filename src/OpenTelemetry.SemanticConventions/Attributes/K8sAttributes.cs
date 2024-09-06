// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class K8sAttributes
{
    /// <summary>
    /// The name of the cluster
    /// </summary>
    public const string AttributeK8sClusterName = "k8s.cluster.name";

    /// <summary>
    /// A pseudo-ID for the cluster, set to the UID of the <c>kube-system</c> namespace
    /// </summary>
    /// <remarks>
    /// K8s doesn't have support for obtaining a cluster ID. If this is ever
    /// added, we will recommend collecting the <c>k8s.cluster.uid</c> through the
    /// official APIs. In the meantime, we are able to use the <c>uid</c> of the
    /// <c>kube-system</c> namespace as a proxy for cluster ID. Read on for the
    /// rationale.
    /// <p>
    /// Every object created in a K8s cluster is assigned a distinct UID. The
    /// <c>kube-system</c> namespace is used by Kubernetes itself and will exist
    /// for the lifetime of the cluster. Using the <c>uid</c> of the <c>kube-system</c>
    /// namespace is a reasonable proxy for the K8s ClusterID as it will only
    /// change if the cluster is rebuilt. Furthermore, Kubernetes UIDs are
    /// UUIDs as standardized by
    /// <a href="https://www.itu.int/ITU-T/studygroups/com17/oid.html">ISO/IEC 9834-8 and ITU-T X.667</a>.
    /// Which states:
    /// <p>
    /// <blockquote>
    /// If generated according to one of the mechanisms defined in Rec.
    /// ITU-T X.667 | ISO/IEC 9834-8, a UUID is either guaranteed to be
    /// different from all other UUIDs generated before 3603 A.D., or is
    /// extremely likely to be different (depending on the mechanism chosen).</blockquote>
    /// 
    /// <p>
    /// Therefore, UIDs between clusters should be extremely unlikely to
    /// conflict
    /// </remarks>
    public const string AttributeK8sClusterUid = "k8s.cluster.uid";

    /// <summary>
    /// The name of the Container from Pod specification, must be unique within a Pod. Container runtime usually uses different globally unique name (<c>container.name</c>)
    /// </summary>
    public const string AttributeK8sContainerName = "k8s.container.name";

    /// <summary>
    /// Number of times the container was restarted. This attribute can be used to identify a particular container (running or stopped) within a container spec
    /// </summary>
    public const string AttributeK8sContainerRestartCount = "k8s.container.restart_count";

    /// <summary>
    /// Last terminated reason of the Container
    /// </summary>
    public const string AttributeK8sContainerStatusLastTerminatedReason = "k8s.container.status.last_terminated_reason";

    /// <summary>
    /// The name of the CronJob
    /// </summary>
    public const string AttributeK8sCronjobName = "k8s.cronjob.name";

    /// <summary>
    /// The UID of the CronJob
    /// </summary>
    public const string AttributeK8sCronjobUid = "k8s.cronjob.uid";

    /// <summary>
    /// The name of the DaemonSet
    /// </summary>
    public const string AttributeK8sDaemonsetName = "k8s.daemonset.name";

    /// <summary>
    /// The UID of the DaemonSet
    /// </summary>
    public const string AttributeK8sDaemonsetUid = "k8s.daemonset.uid";

    /// <summary>
    /// The name of the Deployment
    /// </summary>
    public const string AttributeK8sDeploymentName = "k8s.deployment.name";

    /// <summary>
    /// The UID of the Deployment
    /// </summary>
    public const string AttributeK8sDeploymentUid = "k8s.deployment.uid";

    /// <summary>
    /// The name of the Job
    /// </summary>
    public const string AttributeK8sJobName = "k8s.job.name";

    /// <summary>
    /// The UID of the Job
    /// </summary>
    public const string AttributeK8sJobUid = "k8s.job.uid";

    /// <summary>
    /// The name of the namespace that the pod is running in
    /// </summary>
    public const string AttributeK8sNamespaceName = "k8s.namespace.name";

    /// <summary>
    /// The name of the Node
    /// </summary>
    public const string AttributeK8sNodeName = "k8s.node.name";

    /// <summary>
    /// The UID of the Node
    /// </summary>
    public const string AttributeK8sNodeUid = "k8s.node.uid";

    /// <summary>
    /// The annotation key-value pairs placed on the Pod, the <c><key></c> being the annotation name, the value being the annotation value
    /// </summary>
    public const string AttributeK8sPodAnnotationTemplate = "k8s.pod.annotation";

    /// <summary>
    /// The label key-value pairs placed on the Pod, the <c><key></c> being the label name, the value being the label value
    /// </summary>
    public const string AttributeK8sPodLabelTemplate = "k8s.pod.label";

    /// <summary>
    /// Deprecated, use <c>k8s.pod.label</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>k8s.pod.label</c>")]
    public const string AttributeK8sPodLabelsTemplate = "k8s.pod.labels";

    /// <summary>
    /// The name of the Pod
    /// </summary>
    public const string AttributeK8sPodName = "k8s.pod.name";

    /// <summary>
    /// The UID of the Pod
    /// </summary>
    public const string AttributeK8sPodUid = "k8s.pod.uid";

    /// <summary>
    /// The name of the ReplicaSet
    /// </summary>
    public const string AttributeK8sReplicasetName = "k8s.replicaset.name";

    /// <summary>
    /// The UID of the ReplicaSet
    /// </summary>
    public const string AttributeK8sReplicasetUid = "k8s.replicaset.uid";

    /// <summary>
    /// The name of the StatefulSet
    /// </summary>
    public const string AttributeK8sStatefulsetName = "k8s.statefulset.name";

    /// <summary>
    /// The UID of the StatefulSet
    /// </summary>
    public const string AttributeK8sStatefulsetUid = "k8s.statefulset.uid";
}
