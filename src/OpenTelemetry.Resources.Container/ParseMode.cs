// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Container;

/// <summary>
/// CGroup Parse Versions.
/// </summary>
internal enum ParseMode
{
    /// <summary>
    /// Represents CGroupV1.
    /// </summary>
    V1,

    /// <summary>
    /// Represents CGroupV2.
    /// </summary>
    V2,

    /// <summary>
    /// Represents Kubernetes.
    /// </summary>
    K8s,
}
