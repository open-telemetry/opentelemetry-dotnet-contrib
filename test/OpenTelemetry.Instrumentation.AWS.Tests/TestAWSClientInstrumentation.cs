// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon;
using Amazon.Bedrock;
using Amazon.Bedrock.Model;
using Amazon.BedrockAgent;
using Amazon.BedrockAgent.Model;
using Amazon.BedrockAgentRuntime;
using Amazon.BedrockAgentRuntime.Model;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

public class TestAWSClientInstrumentation
{
    private static readonly string[] ExpectedDynamoTableNames = ["SampleProduct"];

    [Fact]
#if NETFRAMEWORK
    public void TestDDBScanSuccessful()
#else
    public async Task TestDDBScanSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(ddb, "{}", requestId, true);
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = ["Id", "Name"],
            };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            await ddb.ScanAsync(scan_request);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new TestAmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(ddb, "{}", requestId, true);
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = ["Id", "Name"],
            };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            await ddb.ScanAsync(scan_request);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var amazonServiceException = new AmazonServiceException
            {
                StatusCode = System.Net.HttpStatusCode.NotFound,
                RequestId = requestId,
            };
            CustomResponses.SetResponse(ddb, (request) => { throw amazonServiceException; });
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = ["Id", "Name"],
            };

            try
            {
#if NETFRAMEWORK
                ddb.Scan(scan_request);
#else
                await ddb.ScanAsync(scan_request);
#endif
            }
            catch (AmazonServiceException ex)
            {
                Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        Assert.NotEmpty(exportedItems);

        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(ActivityStatusCode.Error, awssdk_activity.Status);
        Assert.Equal("Exception of type 'Amazon.Runtime.AmazonServiceException' was thrown.", awssdk_activity.StatusDescription);
        Assert.Equal("exception", awssdk_activity.Events.First().Name);
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSQSSendMessageSuccessfulSampled()
#else
    public async Task TestSQSSendMessageSuccessfulSampled()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        SendMessageRequest send_msg_req;

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(sqs, dummyResponse, requestId, true);
            send_msg_req = new SendMessageRequest
            {
                QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                MessageBody = "Hello from OT",
                MessageAttributes = [],
            };
            send_msg_req.MessageAttributes.Add("Custom", new MessageAttributeValue { StringValue = "Value", DataType = "String" });
#if NETFRAMEWORK
            sqs.SendMessage(send_msg_req);
#else
            await sqs.SendMessageAsync(send_msg_req);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "SQS.SendMessage");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateSqsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));

        Assert.Equal(2, send_msg_req.MessageAttributes.Count);
        Assert.Contains(
            send_msg_req.MessageAttributes,
            kv => kv.Key == "traceparent" && kv.Value.StringValue == $"00-{awssdk_activity.TraceId}-{awssdk_activity.SpanId}-01");
        Assert.Contains(
            send_msg_req.MessageAttributes,
            kv => kv.Key == "Custom" && kv.Value.StringValue == "Value");
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSQSSendMessageSuccessfulNotSampled()
#else
    public async Task TestSQSSendMessageSuccessfulNotSampled()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        SendMessageRequest send_msg_req;

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOffSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(sqs, dummyResponse, requestId, true);
            send_msg_req = new SendMessageRequest
            {
                QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                MessageBody = "Hello from OT",
                MessageAttributes = [],
            };
            send_msg_req.MessageAttributes.Add("Custom", new MessageAttributeValue { StringValue = "Value", DataType = "String" });
#if NETFRAMEWORK
            sqs.SendMessage(send_msg_req);
#else
            await sqs.SendMessageAsync(send_msg_req);
#endif
        }

        Assert.Empty(exportedItems);

        Assert.Equal(2, send_msg_req.MessageAttributes.Count);
        Assert.Contains(
            send_msg_req.MessageAttributes,
            kv => kv.Key == "traceparent" && kv.Value.StringValue == $"00-{parent.TraceId}-{parent.SpanId}-00");
        Assert.Contains(
            send_msg_req.MessageAttributes,
            kv => kv.Key == "Custom" && kv.Value.StringValue == "Value");
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSNSPublishSuccessful()
#else
    public async Task TestSNSPublishSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                  .AddXRayTraceId()
                  .SetSampler(new AlwaysOnSampler())
                  .AddAWSInstrumentation(o => o.SemanticConventionVersion = SemanticConventionVersion.Latest)
                  .AddInMemoryExporter(exportedItems)
                  .Build())
        {
            var sns = new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = """
                <PublishResponse xmlns="https://sns.amazonaws.com/doc/2010-03-31/">
                  <PublishResult>
                    <MessageId>567910cd-659e-55d4-bc19-f29d9g3b2378</MessageId>
                  </PublishResult>
                  <ResponseMetadata>
                    <RequestId>fakerequ-esti-dfak-ereq-uestidfakere</RequestId>
                  </ResponseMetadata>
                </PublishResponse>
                """;
            CustomResponses.SetResponse(sns, dummyResponse, requestId, true);
            var publishRequest = new Amazon.SimpleNotificationService.Model.PublishRequest
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789:MyTestTopic",
                Message = "Hello from OT",
            };
#if NETFRAMEWORK
            sns.Publish(publishRequest);
#else
            await sns.PublishAsync(publishRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var activity = exportedItems.FirstOrDefault(e => e.DisplayName == "SNS.Publish");
        Assert.NotNull(activity);

        this.ValidateAWSActivity(activity, parent);
        this.ValidateSnsActivityTags(activity);

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockGetGuardrailSuccessful()
#else
    public async Task TestBedrockGetGuardrailSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrock = new AmazonBedrockClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{\"GuardrailId\":\"123456789\"}";
            CustomResponses.SetResponse(bedrock, dummyResponse, requestId, true);
            var getGuardrailRequest = new GetGuardrailRequest { GuardrailIdentifier = "123456789" };
#if NETFRAMEWORK
            bedrock.GetGuardrail(getGuardrailRequest);
#else
            await bedrock.GetGuardrailAsync(getGuardrailRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock.GetGuardrail");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockruntime = new AmazonBedrockRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest { ModelId = "amazon.titan-text-express-v1" };
#if NETFRAMEWORK
            var response = bedrockruntime.InvokeModel(invokeModelRequest);
#else
            var response = await bedrockruntime.InvokeModelAsync(invokeModelRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Runtime.InvokeModel");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentGetAgentSuccessful()
#else
    public async Task TestBedrockAgentGetAgentSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockagent = new AmazonBedrockAgentClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, true);
            var getAgentRequest = new GetAgentRequest { AgentId = "1234567890" };
#if NETFRAMEWORK
            var response = bedrockagent.GetAgent(getAgentRequest);
#else
            var response = await bedrockagent.GetAgentAsync(getAgentRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetAgent");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentAgentOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentGetKnowledgeBaseSuccessful()
#else
    public async Task TestBedrockAgentGetKnowledgeBaseSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockagent = new AmazonBedrockAgentClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, true);
            var getKnowledgeBaseRequest = new GetKnowledgeBaseRequest { KnowledgeBaseId = "1234567890" };
#if NETFRAMEWORK
            var response = bedrockagent.GetKnowledgeBase(getKnowledgeBaseRequest);
#else
            var response = await bedrockagent.GetKnowledgeBaseAsync(getKnowledgeBaseRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetKnowledgeBase");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentKnowledgeBaseOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentGetDataSourceSuccessful()
#else
    public async Task TestBedrockAgentGetDataSourceSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockagent = new AmazonBedrockAgentClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, true);
            var getDataSourceRequest = new GetDataSourceRequest { DataSourceId = "1234567890", KnowledgeBaseId = "1234567890", };
#if NETFRAMEWORK
            var response = bedrockagent.GetDataSource(getDataSourceRequest);
#else
            var response = await bedrockagent.GetDataSourceAsync(getDataSourceRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetDataSource");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentDataSourceOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentRuntimeInvokeAgentSuccessful()
#else
    public async Task TestBedrockAgentRuntimeInvokeAgentSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockAgentRuntimeClient = new AmazonBedrockAgentRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockAgentRuntimeClient, dummyResponse, requestId, true);
            var invokeAgentRequest = new InvokeAgentRequest
            {
                AgentId = "123456789",
                AgentAliasId = "testalias",
                SessionId = "test-session-id",
                InputText = "sample input text",
            };
#if NETFRAMEWORK
            var response = bedrockAgentRuntimeClient.InvokeAgent(invokeAgentRequest);
#else
            var response = await bedrockAgentRuntimeClient.InvokeAgentAsync(invokeAgentRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent Runtime.InvokeAgent");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentRuntimeAgentOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentRuntimeRetrieveSuccessful()
#else
    public async Task TestBedrockAgentRuntimeRetrieveSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockagentruntime = new AmazonBedrockAgentRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagentruntime, dummyResponse, requestId, true);
            var retrieveRequest = new RetrieveRequest { KnowledgeBaseId = "123456789" };
#if NETFRAMEWORK
            var response = bedrockagentruntime.Retrieve(retrieveRequest);
#else
            var response = await bedrockagentruntime.RetrieveAsync(retrieveRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent Runtime.Retrieve");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestS3PutObjectSuccessful()
#else
    public async Task TestS3PutObjectSuccessful()
#endif
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation(o =>
                   {
                       o.SemanticConventionVersion = SemanticConventionVersion.Latest;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var s3 = new Amazon.S3.AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(s3, "{}", requestId, true);
            var putRequest = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = "my-test-bucket",
                Key = "test-key",
                ContentBody = "test content",
            };
#if NETFRAMEWORK
            s3.PutObject(putRequest);
#else
            await s3.PutObjectAsync(putRequest);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "S3.PutObject");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateS3ActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    private void ValidateAWSActivity(Activity aws_activity, Activity parent)
    {
        Assert.Equal(parent.SpanId, aws_activity.ParentSpanId);
        Assert.Equal(ActivityKind.Client, aws_activity.Kind);
    }

    private void ValidateDynamoActivityTags(Activity ddb_activity)
    {
        Assert.Equal("DynamoDB.Scan", ddb_activity.DisplayName);
        Assert.Equal(ExpectedDynamoTableNames, Utils.GetTagValue(ddb_activity, "aws.dynamodb.table_names"));
        Assert.Equal("dynamodb", Utils.GetTagValue(ddb_activity, "db.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(ddb_activity, "rpc.system"));
        Assert.Equal("DynamoDB", Utils.GetTagValue(ddb_activity, "rpc.service"));
        Assert.Equal("Scan", Utils.GetTagValue(ddb_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(ddb_activity, "cloud.region"));
    }

    private void ValidateSqsActivityTags(Activity sqs_activity)
    {
        Assert.Equal("SQS.SendMessage", sqs_activity.DisplayName);
        Assert.Equal("https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue", Utils.GetTagValue(sqs_activity, "aws.sqs.queue.url"));
        Assert.Equal("aws-api", Utils.GetTagValue(sqs_activity, "rpc.system"));
        Assert.Equal("SQS", Utils.GetTagValue(sqs_activity, "rpc.service"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(sqs_activity, "cloud.region"));
    }

    private void ValidateSnsActivityTags(Activity sns_activity)
    {
        Assert.Equal("SNS.Publish", sns_activity.DisplayName);
        Assert.Equal("arn:aws:sns:us-east-1:123456789:MyTestTopic", Utils.GetTagValue(sns_activity, "aws.sns.topic.arn"));
        Assert.Equal("aws-api", Utils.GetTagValue(sns_activity, "rpc.system"));
        Assert.Equal("SNS", Utils.GetTagValue(sns_activity, "rpc.service"));
        Assert.Equal("Publish", Utils.GetTagValue(sns_activity, "rpc.method"));
    }

    private void ValidateBedrockActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock.GetGuardrail", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.guardrail.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetGuardrail", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockRuntimeActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Runtime.InvokeModel", bedrock_activity.DisplayName);
        Assert.Equal("amazon.titan-text-express-v1", Utils.GetTagValue(bedrock_activity, "gen_ai.request.model"));
        Assert.Equal("aws.bedrock", Utils.GetTagValue(bedrock_activity, "gen_ai.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeModel", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetAgent", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetKnowledgeBase", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetKnowledgeBase", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentDataSourceOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetDataSource", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.data_source.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetDataSource", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentRuntimeAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.InvokeAgent", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.Retrieve", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("Retrieve", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateS3ActivityTags(Activity s3_activity)
    {
        Assert.Equal("S3.PutObject", s3_activity.DisplayName);
        Assert.Equal("my-test-bucket", Utils.GetTagValue(s3_activity, "aws.s3.bucket"));
        Assert.Equal("test-key", Utils.GetTagValue(s3_activity, "aws.s3.key"));
        Assert.Equal("aws-api", Utils.GetTagValue(s3_activity, "rpc.system"));
        Assert.Equal("S3", Utils.GetTagValue(s3_activity, "rpc.service"));
        Assert.Equal("PutObject", Utils.GetTagValue(s3_activity, "rpc.method"));
    }
}
