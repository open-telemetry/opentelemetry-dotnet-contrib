// <copyright file="AWSServiceType.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceType
{
    internal const string DynamoDbService = "DynamoDBv2";
    internal const string SQSService = "SQS";
    internal const string SNSService = "SimpleNotificationService"; // SNS

    internal static bool IsDynamoDbService(string service)
        => DynamoDbService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSqsService(string service)
        => SQSService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSnsService(string service)
        => SNSService.Equals(service, StringComparison.OrdinalIgnoreCase);
}
