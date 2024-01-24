// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceType
{
    internal const string DynamoDbService = "DynamoDB";
    internal const string SQSService = "SQS";
    internal const string SNSService = "SNS";

    internal static bool IsDynamoDbService(string service)
        => DynamoDbService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSqsService(string service)
        => SQSService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSnsService(string service)
        => SNSService.Equals(service, StringComparison.OrdinalIgnoreCase);
}
