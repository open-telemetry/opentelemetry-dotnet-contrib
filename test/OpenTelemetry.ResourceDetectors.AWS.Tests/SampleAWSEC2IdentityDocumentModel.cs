// <copyright file="SampleAWSEC2IdentityDocumentModel.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.ResourceDetectors.AWS.Models;

namespace OpenTelemetry.ResourceDetectors.AWS.Tests;

internal class SampleAWSEC2IdentityDocumentModel : AWSEC2IdentityDocumentModel
{
    public SampleAWSEC2IdentityDocumentModel()
    {
        this.AccountId = "Test account id";
        this.AvailabilityZone = "Test availability zone";
        this.Region = "Test aws region";
        this.InstanceId = "Test instance id";
        this.InstanceType = "Test instance type";
    }
}
