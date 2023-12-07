// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.ResourceDetectors.AWS.Models;

internal class AWSEC2IdentityDocumentModel
{
    public string? AccountId { get; set; }

    public string? AvailabilityZone { get; set; }

    public string? Region { get; set; }

    public string? InstanceId { get; set; }

    public string? InstanceType { get; set; }
}
