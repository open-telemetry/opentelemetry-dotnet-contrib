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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Moq;
using OpenTelemetry.Trace;
using Xunit;
using Status = OpenTelemetry.Trace.Status;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests
{
    public class TestAWSClientInstrumentation
    {
        [Fact]
        public void TestS3()
        {
            var processor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent").Start();

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddAWSInstrumentation()
                .AddProcessor(processor.Object)
                .Build())
            {
                var s3 = new AmazonS3Client();
#if NET452
                ListObjectsV2Request req = new ListObjectsV2Request
                {
                    BucketName = "srprash-test-bucket",
                    MaxKeys = 10,
                };
                var res = s3.ListObjectsV2(req);
#else
                s3.ListBucketsAsync().Wait();
#endif
            }

            var curr_activity = Activity.Current;

            var invocation_count = processor.Invocations.Count;
            var activity_0 = processor.Invocations[0].Arguments;
            var activity_1 = processor.Invocations[1].Arguments;
            var activity_2 = processor.Invocations[2].Arguments;
        }

        [Fact]
        public void TestDDB()
        {
            var processor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent").Start();

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddAWSInstrumentation()
                .AddProcessor(processor.Object)
                .Build())
            {
                var ddb = new AmazonDynamoDBClient();
                var scan_request = new ScanRequest();

                // scan_request.TableName = "SampleProduct";
                scan_request.TableName = "FakeTable";
                scan_request.AttributesToGet = new List<string>() { "Id", "Name" };
#if NET452
                // ddb.ListTables();

                ScanResponse resp = ddb.Scan(scan_request);
#else
                // ddb.ListTablesAsync();

                ddb.ScanAsync(scan_request);
#endif

                var curr_activity = Activity.Current;
                var activity_0 = processor.Invocations[0].Arguments;
            }
        }

        [Fact]
        public void TestSQS()
        {
            var processor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent").Start();

            using (Sdk.CreateTracerProviderBuilder()
                .AddXRayActivityTraceIdGenerator()
                .SetSampler(new AlwaysOnSampler())
                .AddAWSInstrumentation()
                .AddProcessor(processor.Object)
                .Build())
            {
                var sqs = new AmazonSQSClient();
                var send_msg_req = new SendMessageRequest();
                send_msg_req.QueueUrl = "https://sqs.us-west-2.amazonaws.com/702258172533/traceLinkingQueue";
                send_msg_req.MessageBody = "Hello from OT";
#if NET452
                sqs.SendMessage(send_msg_req);
#else
                sqs.SendMessageAsync(send_msg_req);
#endif

                var curr_activity = Activity.Current;
                var activity_0 = processor.Invocations[0].Arguments;
            }
        }
    }
}
