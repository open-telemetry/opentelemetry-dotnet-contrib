// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

public class TestAWSClientInstrumentation
{
    [Fact]
#if NETFRAMEWORK
    public void TestDDBScanSuccessful()
#else
    public async Task TestDDBScanSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            CustomResponses.SetResponse(ddb, null, requestId, true);
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = new List<string> { "Id", "Name" },
            };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            await ddb.ScanAsync(scan_request);
#endif
            Assert.Single(exportedItems);

            Activity awssdk_activity = exportedItems[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateDynamoActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

    [Fact]
#if NETFRAMEWORK
    public void TestDDBSubtypeScanSuccessful()
#else
    public async Task TestDDBSubtypeScanSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new TestAmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            CustomResponses.SetResponse(ddb, null, requestId, true);
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = new List<string>() { "Id", "Name" },
            };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            await ddb.ScanAsync(scan_request);
#endif
            Assert.Single(exportedItems);

            Activity awssdk_activity = exportedItems[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateDynamoActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

    [Fact]
#if NETFRAMEWORK
    public void TestDDBScanUnsuccessful()
#else
    public async Task TestDDBScanUnsuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            AmazonServiceException amazonServiceException = new AmazonServiceException();
            amazonServiceException.StatusCode = System.Net.HttpStatusCode.NotFound;
            amazonServiceException.RequestId = requestId;
            CustomResponses.SetResponse(ddb, (request) => { throw amazonServiceException; });
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = new List<string>() { "Id", "Name" },
            };

            try
            {
#if NETFRAMEWORK
                ddb.Scan(scan_request);
#else
                await ddb.ScanAsync(scan_request);
#endif
            }
            catch (AmazonServiceException)
            {
                Assert.Single(exportedItems);

                Activity awssdk_activity = exportedItems[0];

                this.ValidateAWSActivity(awssdk_activity, parent);
                this.ValidateDynamoActivityTags(awssdk_activity);

                Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
                Assert.Equal(Status.Error.WithDescription("Exception of type 'Amazon.Runtime.AmazonServiceException' was thrown."), awssdk_activity.GetStatus());
                Assert.Equal("exception", awssdk_activity.Events.First().Name);
            }
        }
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSQSSendMessageSuccessful()
#else
    public async Task TestSQSSendMessageSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(sqs, dummyResponse, requestId, true);
            var send_msg_req = new SendMessageRequest();
            send_msg_req.QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue";
            send_msg_req.MessageBody = "Hello from OT";
            send_msg_req.MessageAttributes.Add("Custom", new MessageAttributeValue { StringValue = "Value", DataType = "String" });
#if NETFRAMEWORK
            sqs.SendMessage(send_msg_req);
#else
            await sqs.SendMessageAsync(send_msg_req);
#endif
            Assert.Single(exportedItems);
            Activity awssdk_activity = exportedItems[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateSqsActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

    [Fact]
#if NETFRAMEWORK
    public void TestS3PutBucketSuccessful()
#else
    public async Task TestS3PutBucketSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var s3 = new AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(s3, dummyResponse, requestId, true);
            var put_bucket_req = new PutBucketRequest();
            put_bucket_req.BucketName = "MyTestBucket";
#if NETFRAMEWORK
            s3.PutBucket(put_bucket_req);
#else
            await s3.PutBucketAsync(put_bucket_req);
#endif
            Assert.Single(exportedItems);
            Activity awssdk_activity = exportedItems[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateS3ActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

    [Fact]
#if NETFRAMEWORK
    public void TestKinesisCreateStreamSuccessful()
#else
    public async Task TestKinesisCreateStreamSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var kinesis = new AmazonKinesisClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(kinesis, dummyResponse, requestId, true);
            var create_stream_req = new CreateStreamRequest();
            create_stream_req.StreamName = "MyTestStream";
#if NETFRAMEWORK
            kinesis.CreateStream(create_stream_req);
#else
            await kinesis.CreateStreamAsync(create_stream_req);
#endif
            Assert.Single(exportedItems);
            Activity awssdk_activity = exportedItems[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateKinesisActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

    private void ValidateAWSActivity(Activity aws_activity, Activity parent)
    {
        Assert.Equal(parent.SpanId, aws_activity.ParentSpanId);
        Assert.Equal(ActivityKind.Client, aws_activity.Kind);
    }

    private void ValidateDynamoActivityTags(Activity ddb_activity)
    {
        Assert.Equal("DynamoDB.Scan", ddb_activity.DisplayName);
        Assert.Equal("DynamoDB", Utils.GetTagValue(ddb_activity, "aws.service"));
        Assert.Equal("Scan", Utils.GetTagValue(ddb_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(ddb_activity, "aws.region"));
        Assert.Equal("SampleProduct", Utils.GetTagValue(ddb_activity, "aws.table_name"));
        Assert.Equal("dynamodb", Utils.GetTagValue(ddb_activity, "db.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(ddb_activity, "rpc.system"));
        Assert.Equal("DynamoDB", Utils.GetTagValue(ddb_activity, "rpc.service"));
        Assert.Equal("Scan", Utils.GetTagValue(ddb_activity, "rpc.method"));
    }

    private void ValidateSqsActivityTags(Activity sqs_activity)
    {
        Assert.Equal("SQS.SendMessage", sqs_activity.DisplayName);
        Assert.Equal("SQS", Utils.GetTagValue(sqs_activity, "aws.service"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(sqs_activity, "aws.region"));
        Assert.Equal("https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue", Utils.GetTagValue(sqs_activity, "aws.queue_url"));
        Assert.Equal("aws-api", Utils.GetTagValue(sqs_activity, "rpc.system"));
        Assert.Equal("SQS", Utils.GetTagValue(sqs_activity, "rpc.service"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "rpc.method"));
    }

    private void ValidateS3ActivityTags(Activity s3_activity)
    {
        Assert.Equal("S3.PutBucket", s3_activity.DisplayName);
        Assert.Equal("S3", Utils.GetTagValue(s3_activity, "aws.service"));
        Assert.Equal("PutBucket", Utils.GetTagValue(s3_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(s3_activity, "aws.region"));
        Assert.Equal("MyTestBucket", Utils.GetTagValue(s3_activity, "aws.s3.bucket"));
        Assert.Equal("aws-api", Utils.GetTagValue(s3_activity, "rpc.system"));
        Assert.Equal("S3", Utils.GetTagValue(s3_activity, "rpc.service"));
        Assert.Equal("PutBucket", Utils.GetTagValue(s3_activity, "rpc.method"));
    }

    private void ValidateKinesisActivityTags(Activity kinesis_activity)
    {
        Assert.Equal("Kinesis.CreateStream", kinesis_activity.DisplayName);
        Assert.Equal("Kinesis", Utils.GetTagValue(kinesis_activity, "aws.service"));
        Assert.Equal("CreateStream", Utils.GetTagValue(kinesis_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(kinesis_activity, "aws.region"));
        Assert.Equal("MyTestStream", Utils.GetTagValue(kinesis_activity, "aws.kinesis.stream_name"));
        Assert.Equal("aws-api", Utils.GetTagValue(kinesis_activity, "rpc.system"));
        Assert.Equal("Kinesis", Utils.GetTagValue(kinesis_activity, "rpc.service"));
        Assert.Equal("CreateStream", Utils.GetTagValue(kinesis_activity, "rpc.method"));
    }
}
