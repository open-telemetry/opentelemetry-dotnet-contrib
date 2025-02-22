// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class AwsAttributes
{
    /// <summary>
    /// The JSON-serialized value of each item in the <c>AttributeDefinitions</c> request field.
    /// </summary>
    public const string AttributeAwsDynamodbAttributeDefinitions = "aws.dynamodb.attribute_definitions";

    /// <summary>
    /// The value of the <c>AttributesToGet</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbAttributesToGet = "aws.dynamodb.attributes_to_get";

    /// <summary>
    /// The value of the <c>ConsistentRead</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbConsistentRead = "aws.dynamodb.consistent_read";

    /// <summary>
    /// The JSON-serialized value of each item in the <c>ConsumedCapacity</c> response field.
    /// </summary>
    public const string AttributeAwsDynamodbConsumedCapacity = "aws.dynamodb.consumed_capacity";

    /// <summary>
    /// The value of the <c>Count</c> response parameter.
    /// </summary>
    public const string AttributeAwsDynamodbCount = "aws.dynamodb.count";

    /// <summary>
    /// The value of the <c>ExclusiveStartTableName</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbExclusiveStartTable = "aws.dynamodb.exclusive_start_table";

    /// <summary>
    /// The JSON-serialized value of each item in the <c>GlobalSecondaryIndexUpdates</c> request field.
    /// </summary>
    public const string AttributeAwsDynamodbGlobalSecondaryIndexUpdates = "aws.dynamodb.global_secondary_index_updates";

    /// <summary>
    /// The JSON-serialized value of each item of the <c>GlobalSecondaryIndexes</c> request field.
    /// </summary>
    public const string AttributeAwsDynamodbGlobalSecondaryIndexes = "aws.dynamodb.global_secondary_indexes";

    /// <summary>
    /// The value of the <c>IndexName</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbIndexName = "aws.dynamodb.index_name";

    /// <summary>
    /// The JSON-serialized value of the <c>ItemCollectionMetrics</c> response field.
    /// </summary>
    public const string AttributeAwsDynamodbItemCollectionMetrics = "aws.dynamodb.item_collection_metrics";

    /// <summary>
    /// The value of the <c>Limit</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbLimit = "aws.dynamodb.limit";

    /// <summary>
    /// The JSON-serialized value of each item of the <c>LocalSecondaryIndexes</c> request field.
    /// </summary>
    public const string AttributeAwsDynamodbLocalSecondaryIndexes = "aws.dynamodb.local_secondary_indexes";

    /// <summary>
    /// The value of the <c>ProjectionExpression</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbProjection = "aws.dynamodb.projection";

    /// <summary>
    /// The value of the <c>ProvisionedThroughput.ReadCapacityUnits</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbProvisionedReadCapacity = "aws.dynamodb.provisioned_read_capacity";

    /// <summary>
    /// The value of the <c>ProvisionedThroughput.WriteCapacityUnits</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbProvisionedWriteCapacity = "aws.dynamodb.provisioned_write_capacity";

    /// <summary>
    /// The value of the <c>ScanIndexForward</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbScanForward = "aws.dynamodb.scan_forward";

    /// <summary>
    /// The value of the <c>ScannedCount</c> response parameter.
    /// </summary>
    public const string AttributeAwsDynamodbScannedCount = "aws.dynamodb.scanned_count";

    /// <summary>
    /// The value of the <c>Segment</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbSegment = "aws.dynamodb.segment";

    /// <summary>
    /// The value of the <c>Select</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbSelect = "aws.dynamodb.select";

    /// <summary>
    /// The number of items in the <c>TableNames</c> response parameter.
    /// </summary>
    public const string AttributeAwsDynamodbTableCount = "aws.dynamodb.table_count";

    /// <summary>
    /// The keys in the <c>RequestItems</c> object field.
    /// </summary>
    public const string AttributeAwsDynamodbTableNames = "aws.dynamodb.table_names";

    /// <summary>
    /// The value of the <c>TotalSegments</c> request parameter.
    /// </summary>
    public const string AttributeAwsDynamodbTotalSegments = "aws.dynamodb.total_segments";

    /// <summary>
    /// The ARN of an <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/clusters.html">ECS cluster</a>.
    /// </summary>
    public const string AttributeAwsEcsClusterArn = "aws.ecs.cluster.arn";

    /// <summary>
    /// The Amazon Resource Name (ARN) of an <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ECS_instances.html">ECS container instance</a>.
    /// </summary>
    public const string AttributeAwsEcsContainerArn = "aws.ecs.container.arn";

    /// <summary>
    /// The <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/launch_types.html">launch type</a> for an ECS task.
    /// </summary>
    public const string AttributeAwsEcsLaunchtype = "aws.ecs.launchtype";

    /// <summary>
    /// The ARN of a running <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-account-settings.html#ecs-resource-ids">ECS task</a>.
    /// </summary>
    public const string AttributeAwsEcsTaskArn = "aws.ecs.task.arn";

    /// <summary>
    /// The family name of the <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task_definitions.html">ECS task definition</a> used to create the ECS task.
    /// </summary>
    public const string AttributeAwsEcsTaskFamily = "aws.ecs.task.family";

    /// <summary>
    /// The ID of a running ECS task. The ID MUST be extracted from <c>task.arn</c>.
    /// </summary>
    public const string AttributeAwsEcsTaskId = "aws.ecs.task.id";

    /// <summary>
    /// The revision for the task definition used to create the ECS task.
    /// </summary>
    public const string AttributeAwsEcsTaskRevision = "aws.ecs.task.revision";

    /// <summary>
    /// The ARN of an EKS cluster.
    /// </summary>
    public const string AttributeAwsEksClusterArn = "aws.eks.cluster.arn";

    /// <summary>
    /// The AWS extended request ID as returned in the response header <c>x-amz-id-2</c>.
    /// </summary>
    public const string AttributeAwsExtendedRequestId = "aws.extended_request_id";

    /// <summary>
    /// The full invoked ARN as provided on the <c>Context</c> passed to the function (<c>Lambda-Runtime-Invoked-Function-Arn</c> header on the <c>/runtime/invocation/next</c> applicable).
    /// </summary>
    /// <remarks>
    /// This may be different from <c>cloud.resource_id</c> if an alias is involved.
    /// </remarks>
    public const string AttributeAwsLambdaInvokedArn = "aws.lambda.invoked_arn";

    /// <summary>
    /// The Amazon Resource Name(s) (ARN) of the AWS log group(s).
    /// </summary>
    /// <remarks>
    /// See the <a href="https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/iam-access-control-overview-cwl.html#CWL_ARN_Format">log group ARN format documentation</a>.
    /// </remarks>
    public const string AttributeAwsLogGroupArns = "aws.log.group.arns";

    /// <summary>
    /// The name(s) of the AWS log group(s) an application is writing to.
    /// </summary>
    /// <remarks>
    /// Multiple log groups must be supported for cases like multi-container applications, where a single application has sidecar containers, and each write to their own log group.
    /// </remarks>
    public const string AttributeAwsLogGroupNames = "aws.log.group.names";

    /// <summary>
    /// The ARN(s) of the AWS log stream(s).
    /// </summary>
    /// <remarks>
    /// See the <a href="https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/iam-access-control-overview-cwl.html#CWL_ARN_Format">log stream ARN format documentation</a>. One log group can contain several log streams, so these ARNs necessarily identify both a log group and a log stream.
    /// </remarks>
    public const string AttributeAwsLogStreamArns = "aws.log.stream.arns";

    /// <summary>
    /// The name(s) of the AWS log stream(s) an application is writing to.
    /// </summary>
    public const string AttributeAwsLogStreamNames = "aws.log.stream.names";

    /// <summary>
    /// The AWS request ID as returned in the response headers <c>x-amzn-requestid</c>, <c>x-amzn-request-id</c> or <c>x-amz-request-id</c>.
    /// </summary>
    public const string AttributeAwsRequestId = "aws.request_id";

    /// <summary>
    /// The S3 bucket name the request refers to. Corresponds to the <c>--bucket</c> parameter of the <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/index.html">S3 API</a> operations.
    /// </summary>
    /// <remarks>
    /// The <c>bucket</c> attribute is applicable to all S3 operations that reference a bucket, i.e. that require the bucket name as a mandatory parameter.
    /// This applies to almost all S3 operations except <c>list-buckets</c>.
    /// </remarks>
    public const string AttributeAwsS3Bucket = "aws.s3.bucket";

    /// <summary>
    /// The source object (in the form <c>bucket</c>/<c>key</c>) for the copy operation.
    /// </summary>
    /// <remarks>
    /// The <c>copy_source</c> attribute applies to S3 copy operations and corresponds to the <c>--copy-source</c> parameter
    /// of the <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/copy-object.html">copy-object operation within the S3 API</a>.
    /// This applies in particular to the following operations:
    /// <ul>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/copy-object.html">copy-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part-copy.html">upload-part-copy</a>.</li>
    /// </ul>
    /// </remarks>
    public const string AttributeAwsS3CopySource = "aws.s3.copy_source";

    /// <summary>
    /// The delete request container that specifies the objects to be deleted.
    /// </summary>
    /// <remarks>
    /// The <c>delete</c> attribute is only applicable to the <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/delete-object.html">delete-object</a> operation.
    /// The <c>delete</c> attribute corresponds to the <c>--delete</c> parameter of the
    /// <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/delete-objects.html">delete-objects operation within the S3 API</a>.
    /// </remarks>
    public const string AttributeAwsS3Delete = "aws.s3.delete";

    /// <summary>
    /// The S3 object key the request refers to. Corresponds to the <c>--key</c> parameter of the <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/index.html">S3 API</a> operations.
    /// </summary>
    /// <remarks>
    /// The <c>key</c> attribute is applicable to all object-related S3 operations, i.e. that require the object key as a mandatory parameter.
    /// This applies in particular to the following operations:
    /// <ul>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/copy-object.html">copy-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/delete-object.html">delete-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/get-object.html">get-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/head-object.html">head-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/put-object.html">put-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/restore-object.html">restore-object</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/select-object-content.html">select-object-content</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/abort-multipart-upload.html">abort-multipart-upload</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/complete-multipart-upload.html">complete-multipart-upload</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/create-multipart-upload.html">create-multipart-upload</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/list-parts.html">list-parts</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part.html">upload-part</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part-copy.html">upload-part-copy</a>.</li>
    /// </ul>
    /// </remarks>
    public const string AttributeAwsS3Key = "aws.s3.key";

    /// <summary>
    /// The part number of the part being uploaded in a multipart-upload operation. This is a positive integer between 1 and 10,000.
    /// </summary>
    /// <remarks>
    /// The <c>part_number</c> attribute is only applicable to the <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part.html">upload-part</a>
    /// and <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part-copy.html">upload-part-copy</a> operations.
    /// The <c>part_number</c> attribute corresponds to the <c>--part-number</c> parameter of the
    /// <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part.html">upload-part operation within the S3 API</a>.
    /// </remarks>
    public const string AttributeAwsS3PartNumber = "aws.s3.part_number";

    /// <summary>
    /// Upload ID that identifies the multipart upload.
    /// </summary>
    /// <remarks>
    /// The <c>upload_id</c> attribute applies to S3 multipart-upload operations and corresponds to the <c>--upload-id</c> parameter
    /// of the <a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/index.html">S3 API</a> multipart operations.
    /// This applies in particular to the following operations:
    /// <ul>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/abort-multipart-upload.html">abort-multipart-upload</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/complete-multipart-upload.html">complete-multipart-upload</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/list-parts.html">list-parts</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part.html">upload-part</a></li>
    ///   <li><a href="https://docs.aws.amazon.com/cli/latest/reference/s3api/upload-part-copy.html">upload-part-copy</a>.</li>
    /// </ul>
    /// </remarks>
    public const string AttributeAwsS3UploadId = "aws.s3.upload_id";

    /// <summary>
    /// The <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/launch_types.html">launch type</a> for an ECS task.
    /// </summary>
    public static class AwsEcsLaunchtypeValues
    {
        /// <summary>
        /// ec2.
        /// </summary>
        public const string Ec2 = "ec2";

        /// <summary>
        /// fargate.
        /// </summary>
        public const string Fargate = "fargate";
    }
}
