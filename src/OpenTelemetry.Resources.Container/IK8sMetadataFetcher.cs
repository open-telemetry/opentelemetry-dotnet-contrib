// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Container;

internal interface IK8sMetadataFetcher
{
    string? GetApiCredential();

    string? GetContainerName();

    string? GetHostname();

    string? GetPodName();

    string? GetNamespace();

    string? GetServiceBaseUrl();
}
