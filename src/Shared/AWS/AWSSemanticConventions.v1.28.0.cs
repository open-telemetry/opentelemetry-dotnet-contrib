// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.AWS;

// disable Style Warnings to improve readability of this specific file.
#pragma warning disable SA1124
#pragma warning disable SA1005
#pragma warning disable SA1514
#pragma warning disable SA1201
#pragma warning disable SA1516

internal partial class AWSSemanticConventions
{
    /// <summary>
    /// Open Telemetry Semantic Conventions as of 1.28.0:
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.28.0.
    /// </summary>
    private class AWSSemanticConventions_V1_28_0 : AWSSemanticConventionsLegacy
    {
        public override string AttributeAWSDynamoTableName => "aws.dynamodb.table_names";
        public override string AttributeAWSDynamoAttributeDefinitions => "aws.dynamodb.attribute_definitions";
        public override string AttributeAWSDynamoAttributesToGet => "aws.dynamodb.attributes_to_get";
        public override string AttributeAWSDynamoConsistentRead => "aws.dynamodb.consistent_read";
        public override string AttributeAWSDynamoConsumedCapacity => "aws.dynamodb.consumed_capacity";
        public override string AttributeAWSDynamoCount => "aws.dynamodb.count";
        public override string AttributeAWSDynamoExclusiveStartTable => "aws.dynamodb.exclusive_start_table";
        public override string AttributeAWSDynamoGlobalSecondaryIndexUpdates => "aws.dynamodb.global_secondary_index_updates";
        public override string AttributeAWSDynamoGlobalSecondaryIndexes => "aws.dynamodb.global_secondary_indexes";
        public override string AttributeAWSDynamoIndexName => "aws.dynamodb.index_name";
        public override string AttributeAWSDynamoItemCollectionMetrics => "aws.dynamodb.item_collection_metrics";
        public override string AttributeAWSDynamoLimit => "aws.dynamodb.limit";
        public override string AttributeAWSDynamoLocalSecondaryIndexes => "aws.dynamodb.local_secondary_indexes";
        public override string AttributeAWSDynamoProjection => "aws.dynamodb.projection";
        public override string AttributeAWSDynamoProvisionedReadCapacity => "aws.dynamodb.provisioned_read_capacity";
        public override string AttributeAWSDynamoProvisionedWriteCapacity => "aws.dynamodb.provisioned_write_capacity";
        public override string AttributeAWSDynamoScanForward => "aws.dynamodb.scan_forward";
        public override string AttributeAWSDynamoScannedCount => "aws.dynamodb.scanned_count";
        public override string AttributeAWSDynamoSegment => "aws.dynamodb.segment";
        public override string AttributeAWSDynamoSelect => "aws.dynamodb.select";
        public override string AttributeAWSDynamoTableCount => "aws.dynamodb.table_count";
        public override string AttributeAWSDynamoTotalSegments => "aws.dynamodb.total_segments";

        // aws.dynamodb.table_names is specified as string[] in the OTel semantic conventions
        public override IReadOnlyCollection<string> ArrayValueAttributeNames { get; } =
        [
            "aws.dynamodb.attribute_definitions",
            "aws.dynamodb.attributes_to_get",
            "aws.dynamodb.consumed_capacity",
            "aws.dynamodb.global_secondary_index_updates",
            "aws.dynamodb.global_secondary_indexes",
            "aws.dynamodb.local_secondary_indexes",
            "aws.dynamodb.table_names",
            "aws.log.group.arns",
            "aws.log.group.names",
            "aws.log.stream.arns",
            "aws.log.stream.names",
        ];

        // FAAS Attributes
        public override string AttributeFaasID => "cloud.resource_id";
        public override string AttributeFaasExecution => "faas.invocation_id";

        // HTTP Attributes
        [Obsolete("Replaced by <c>http.response.status_code</c>.")]
        public override string AttributeHttpStatusCode => string.Empty; // value no longer written
        [Obsolete("Replaced by <c>url.scheme</c> instead.")]
        public override string AttributeHttpScheme => string.Empty; // value no longer written

        [Obsolete("Split to <c>url.path</c> and `url.query.")]
        public override string AttributeHttpTarget => string.Empty; // value no longer written
        [Obsolete("Replaced by <c>http.request.method</c>.")]
        public override string AttributeHttpMethod => string.Empty; // value no longer written
        public override string AttributeHttpResponseStatusCode => "http.response.status_code";
        public override string AttributeHttpRequestMethod => "http.request.method";

        // NET Attributes
        [Obsolete("Replaced by <c>server.address</c>.")]
        public override string AttributeNetHostName => string.Empty; // value no longer written
        [Obsolete("Replaced by <c>server.port</c>.")]
        public override string AttributeNetHostPort => string.Empty; // value no longer written

        // SERVER Attributes
        public override string AttributeServerAddress => "server.address";
        public override string AttributeServerPort => "server.port";

        // URL Attributes
        public override string AttributeUrlPath => "url.path";
        public override string AttributeUrlQuery => "url.query";
        public override string AttributeUrlScheme => "url.scheme";
    }
}
