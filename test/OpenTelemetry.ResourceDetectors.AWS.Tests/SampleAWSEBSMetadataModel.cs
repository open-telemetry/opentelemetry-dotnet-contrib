// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.ResourceDetectors.AWS.Models;

namespace OpenTelemetry.ResourceDetectors.AWS.Tests;

internal class SampleAWSEBSMetadataModel : AWSEBSMetadataModel
{
    public SampleAWSEBSMetadataModel()
    {
        this.EnvironmentName = "Test environment name";
        this.DeploymentId = "Test ID";
        this.VersionLabel = "Test version label";
    }
}
