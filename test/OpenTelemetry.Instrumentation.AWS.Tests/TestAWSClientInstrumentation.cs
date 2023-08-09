// <copyright file="TestAWSClientInstrumentation.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if !NETFRAMEWORK
using System.Threading.Tasks;
#endif
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Moq;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

public class TestAWSClientInstrumentation
{
    [Fact]
    public void TestDDBScanSuccessful()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            CustomResponses.SetResponse(ddb, null, requestId, true);
            var scan_request = new ScanRequest();

            scan_request.TableName = "SampleProduct";
            scan_request.AttributesToGet = new List<string>() { "Id", "Name" };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            ddb.ScanAsync(scan_request).Wait();
#endif
            var count = processor.Invocations.Count;

            Assert.Equal(3, count);

            Activity awssdk_activity = (Activity)processor.Invocations[2].Arguments[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateDynamoActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

    [Fact]
    public void TestDDBSubtypeScanSuccessful()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
        {
            var ddb = new TestAmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            CustomResponses.SetResponse(ddb, null, requestId, true);
            var scan_request = new ScanRequest();

            scan_request.TableName = "SampleProduct";
            scan_request.AttributesToGet = new List<string>() { "Id", "Name" };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            ddb.ScanAsync(scan_request).Wait();
#endif
            var count = processor.Invocations.Count;

            Assert.Equal(3, count);

            Activity awssdk_activity = (Activity)processor.Invocations[2].Arguments[0];

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
        var processor = new Mock<BaseProcessor<Activity>>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            AmazonServiceException amazonServiceException = new AmazonServiceException();
            amazonServiceException.StatusCode = System.Net.HttpStatusCode.NotFound;
            amazonServiceException.RequestId = requestId;
            CustomResponses.SetResponse(ddb, (request) => { throw amazonServiceException; });
            var scan_request = new ScanRequest();

            scan_request.TableName = "SampleProduct";
            scan_request.AttributesToGet = new List<string>() { "Id", "Name" };

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
                var count = processor.Invocations.Count;
                Assert.Equal(3, count);

                Activity awssdk_activity = (Activity)processor.Invocations[2].Arguments[0];

                this.ValidateAWSActivity(awssdk_activity, parent);
                this.ValidateDynamoActivityTags(awssdk_activity);

                Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
                Assert.Equal(Status.Error.WithDescription("Exception of type 'Amazon.Runtime.AmazonServiceException' was thrown."), awssdk_activity.GetStatus());
                Assert.Equal("exception", awssdk_activity.Events.First().Name);
            }
        }
    }

    [Fact]
    public void TestSQSSendMessageSuccessful()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        var parent = new Activity("parent").Start();

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                                   "<ReceiveMessageResponse>SomeDummyResponse</ReceiveMessageResponse>";
            CustomResponses.SetResponse(sqs, dummyResponse, requestId, true);
            var send_msg_req = new SendMessageRequest();
            send_msg_req.QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue";
            send_msg_req.MessageBody = "Hello from OT";
#if NETFRAMEWORK
            sqs.SendMessage(send_msg_req);
#else
            sqs.SendMessageAsync(send_msg_req).Wait();
#endif

            var count = processor.Invocations.Count;
            Assert.Equal(3, count);
            Activity awssdk_activity = (Activity)processor.Invocations[2].Arguments[0];

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateSqsActivityTags(awssdk_activity);

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
        Assert.Equal("DynamoDBv2.Scan", ddb_activity.DisplayName);
        Assert.Equal("DynamoDBv2", Utils.GetTagValue(ddb_activity, "aws.service"));
        Assert.Equal("Scan", Utils.GetTagValue(ddb_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(ddb_activity, "aws.region"));
        Assert.Equal("SampleProduct", Utils.GetTagValue(ddb_activity, "aws.table_name"));
        Assert.Equal("dynamodb", Utils.GetTagValue(ddb_activity, "db.system"));
    }

    private void ValidateSqsActivityTags(Activity sqs_activity)
    {
        Assert.Equal("SQS.SendMessage", sqs_activity.DisplayName);
        Assert.Equal("SQS", Utils.GetTagValue(sqs_activity, "aws.service"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(sqs_activity, "aws.region"));
        Assert.Equal("https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue", Utils.GetTagValue(sqs_activity, "aws.queue_url"));
    }
}
