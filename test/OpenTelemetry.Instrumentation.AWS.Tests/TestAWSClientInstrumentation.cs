// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
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
            Assert.NotEmpty(exportedItems);

            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateDynamoActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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
            Assert.NotEmpty(exportedItems);

            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateDynamoActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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
                Assert.NotEmpty(exportedItems);

                Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
                Assert.NotNull(awssdk_activity);

                this.ValidateAWSActivity(awssdk_activity, parent);
                this.ValidateDynamoActivityTags(awssdk_activity);

                Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "SQS.SendMessage");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateSqsActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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
        Assert.Equal("SampleProduct", Utils.GetTagValue(ddb_activity, "aws.table_name"));
        Assert.Equal("dynamodb", Utils.GetTagValue(ddb_activity, "db.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(ddb_activity, "rpc.system"));
        Assert.Equal("DynamoDB", Utils.GetTagValue(ddb_activity, "rpc.service"));
        Assert.Equal("Scan", Utils.GetTagValue(ddb_activity, "rpc.method"));
    }

    private void ValidateSqsActivityTags(Activity sqs_activity)
    {
        Assert.Equal("SQS.SendMessage", sqs_activity.DisplayName);
        Assert.Equal("https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue", Utils.GetTagValue(sqs_activity, "aws.queue_url"));
        Assert.Equal("aws-api", Utils.GetTagValue(sqs_activity, "rpc.system"));
        Assert.Equal("SQS", Utils.GetTagValue(sqs_activity, "rpc.service"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "rpc.method"));
    }
}
