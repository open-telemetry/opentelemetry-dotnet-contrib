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
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests
{
    public class TestAWSClientInstrumentation
    {
        [Fact]
        public void TestDDBScanSuccessful()
        {
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddXRayActivityTraceIdGenerator()
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

                ddb.ScanAsync(scan_request);

                var count = processor.Invocations.Count;
                Assert.Equal(2, count);

                Activity awssdk_activity = (Activity)processor.Invocations[0].Arguments[0];

                this.ValidateAWSActivity(awssdk_activity, parent);
                this.ValidateDynamoActivityTags(awssdk_activity);

                Assert.Equal(requestId, awssdk_activity.GetTagValue("aws.requestId"));
            }
        }

        [Fact]
        public void TestDDBScanUnsuccessful()
        {
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddXRayActivityTraceIdGenerator()
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

                ddb.ScanAsync(scan_request);

                var count = processor.Invocations.Count;
                Assert.Equal(2, count);

                Activity awssdk_activity = (Activity)processor.Invocations[0].Arguments[0];

                this.ValidateAWSActivity(awssdk_activity, parent);
                this.ValidateDynamoActivityTags(awssdk_activity);

                Assert.Equal(requestId, awssdk_activity.GetTagValue("aws.requestId"));
                Assert.Equal(Status.Error.WithDescription("Exception of type 'Amazon.Runtime.AmazonServiceException' was thrown."), awssdk_activity.GetStatus());
                Assert.Equal("exception", awssdk_activity.Events.First().Name);
            }
        }

        [Fact]
        public void TestSQSSendMessageSuccessful()
        {
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            using (Sdk.CreateTracerProviderBuilder()
                .AddXRayActivityTraceIdGenerator()
                .SetSampler(new AlwaysOnSampler())
                .AddAWSInstrumentation()
                .AddProcessor(processor.Object)
                .Build())
            {
                var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
                string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
                CustomResponses.SetResponse(sqs, null, requestId, true);
                var send_msg_req = new SendMessageRequest();
                send_msg_req.QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue";
                send_msg_req.MessageBody = "Hello from OT";
                sqs.SendMessageAsync(send_msg_req);

                var count = processor.Invocations.Count;
                Assert.Equal(2, count);
                Activity awssdk_activity = (Activity)processor.Invocations[0].Arguments[0];

                this.ValidateAWSActivity(awssdk_activity, parent);
                this.ValidateSqsActivityTags(awssdk_activity);
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
            Assert.Equal("DynamoDBv2", ddb_activity.GetTagValue("aws.service"));
            Assert.Equal("Scan", ddb_activity.GetTagValue("aws.operation"));
            Assert.Equal("us-east-1", ddb_activity.GetTagValue("aws.region"));
            Assert.Equal("SampleProduct", ddb_activity.GetTagValue("aws.table_name"));
        }

        private void ValidateSqsActivityTags(Activity sqs_activity)
        {
            Assert.Equal("SQS.SendMessage", sqs_activity.DisplayName);
            Assert.Equal("SQS", sqs_activity.GetTagValue("aws.service"));
            Assert.Equal("SendMessage", sqs_activity.GetTagValue("aws.operation"));
            Assert.Equal("us-east-1", sqs_activity.GetTagValue("aws.region"));
            Assert.Equal("https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue", sqs_activity.GetTagValue("aws.queue_url"));
        }
    }
}
