// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceType
{
    internal const string DynamoDbService = "DynamoDB";
    internal const string SQSService = "SQS";
    internal const string SNSService = "SNS";
    internal const string S3Service = "S3";
    internal const string KinesisService = "Kinesis";

    internal static bool IsDynamoDbService(string service)
        => DynamoDbService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSqsService(string service)
        => SQSService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSnsService(string service)
        => SNSService.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsS3Service(string service)
        => S3Service.Equals(service, StringComparison.OrdinalIgnoreCase);

    internal static bool IsKinesisService(string service)
        => KinesisService.Equals(service, StringComparison.OrdinalIgnoreCase);
}
