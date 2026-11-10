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
using Amazon.Runtime.Internal;
using Amazon.Runtime.Telemetry;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Trace;
using AWSTracingPipelineHandler = OpenTelemetry.Instrumentation.AWS.Implementation.AWSTracingPipelineHandler;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

public class TestAWSClientInstrumentation : IDisposable
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(ddb, "{}", requestId, extendedRequestId, true);
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = ["Id", "Name"],
            };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            await ddb.ScanAsync(scan_request, TestContext.Current.CancellationToken);
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(ddb, "{}", requestId, extendedRequestId, true);
            var scan_request = new ScanRequest
            {
                TableName = "SampleProduct",
                AttributesToGet = ["Id", "Name"],
            };
#if NETFRAMEWORK
            ddb.Scan(scan_request);
#else
            await ddb.ScanAsync(scan_request, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";

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
                await ddb.ScanAsync(scan_request, TestContext.Current.CancellationToken);
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            var dummyResponse =
                """
                {
                  "MessageId": "567910cd-659e-55d4-bc19-f29d9g3b2378"
                }
                """;
            CustomResponses.SetResponse(sqs, dummyResponse, requestId, extendedRequestId, true);
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
            await sqs.SendMessageAsync(send_msg_req, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "SQS.SendMessage");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent, ActivityKind.Producer);
        this.ValidateSqsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));

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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            var dummyResponse =
                """
                {
                  "MessageId": "567910cd-659e-55d4-bc19-f29d9g3b2378"
                }
                """;
            CustomResponses.SetResponse(sqs, dummyResponse, requestId, extendedRequestId, true);
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
            await sqs.SendMessageAsync(send_msg_req, TestContext.Current.CancellationToken);
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(sns, dummyResponse, requestId, extendedRequestId, true);
            var publishRequest = new Amazon.SimpleNotificationService.Model.PublishRequest
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789:MyTestTopic",
                Message = "Hello from OT",
            };
#if NETFRAMEWORK
            sns.Publish(publishRequest);
#else
            await sns.PublishAsync(publishRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var activity = exportedItems.FirstOrDefault(e => e.DisplayName == "SNS.Publish");
        Assert.NotNull(activity);

        this.ValidateAWSActivity(activity, parent, ActivityKind.Producer);
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            var dummyResponse = "{\"guardrailId\":\"123456789\"}";
            CustomResponses.SetResponse(bedrock, dummyResponse, requestId, extendedRequestId, true);
            var getGuardrailRequest = new GetGuardrailRequest { GuardrailIdentifier = "123456789" };
#if NETFRAMEWORK
            bedrock.GetGuardrail(getGuardrailRequest);
#else
            await bedrock.GetGuardrailAsync(getGuardrailRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock.GetGuardrail");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, extendedRequestId, true);
            var invokeModelRequest = new InvokeModelRequest { ModelId = "amazon.titan-text-express-v1" };
#if NETFRAMEWORK
            var response = bedrockruntime.InvokeModel(invokeModelRequest);
#else
            var response = await bedrockruntime.InvokeModelAsync(invokeModelRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Runtime.InvokeModel");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, extendedRequestId, true);
            var getAgentRequest = new GetAgentRequest { AgentId = "1234567890" };
#if NETFRAMEWORK
            var response = bedrockagent.GetAgent(getAgentRequest);
#else
            var response = await bedrockagent.GetAgentAsync(getAgentRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetAgent");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentAgentOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, extendedRequestId, true);
            var getKnowledgeBaseRequest = new GetKnowledgeBaseRequest { KnowledgeBaseId = "1234567890" };
#if NETFRAMEWORK
            var response = bedrockagent.GetKnowledgeBase(getKnowledgeBaseRequest);
#else
            var response = await bedrockagent.GetKnowledgeBaseAsync(getKnowledgeBaseRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetKnowledgeBase");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentKnowledgeBaseOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, extendedRequestId, true);
            var getDataSourceRequest = new GetDataSourceRequest { DataSourceId = "1234567890", KnowledgeBaseId = "1234567890", };
#if NETFRAMEWORK
            var response = bedrockagent.GetDataSource(getDataSourceRequest);
#else
            var response = await bedrockagent.GetDataSourceAsync(getDataSourceRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetDataSource");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentDataSourceOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(bedrockAgentRuntimeClient, dummyResponse, requestId, extendedRequestId, true);
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
            var response = await bedrockAgentRuntimeClient.InvokeAgentAsync(invokeAgentRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent Runtime.InvokeAgent");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentRuntimeAgentOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(bedrockagentruntime, dummyResponse, requestId, extendedRequestId, true);
            var retrieveRequest = new RetrieveRequest { KnowledgeBaseId = "1234567890" };
#if NETFRAMEWORK
            var response = bedrockagentruntime.Retrieve(retrieveRequest);
#else
            var response = await bedrockagentruntime.RetrieveAsync(retrieveRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);
        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent Runtime.Retrieve");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
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
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

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
            CustomResponses.SetResponse(s3, "{}", requestId, extendedRequestId, true);
            var putRequest = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = "my-test-bucket",
                Key = "test-key",
                ContentBody = "test content",
            };
#if NETFRAMEWORK
            s3.PutObject(putRequest);
#else
            await s3.PutObjectAsync(putRequest, TestContext.Current.CancellationToken);
#endif
        }

        Assert.NotEmpty(exportedItems);

        var awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "S3.PutObject");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateS3ActivityTags(awssdk_activity);

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(extendedRequestId, Utils.GetTagValue(awssdk_activity, "aws.extended_request_id"));
    }

    [Theory]
    [InlineData(SemanticConventionVersion.V1_28_0, null, null, ActivityKind.Client)]
    [InlineData(SemanticConventionVersion.V1_29_0, null, null, ActivityKind.Client)]
    [InlineData(SemanticConventionVersion.V1_40_0, "messaging.system", "aws_sqs", ActivityKind.Client)]
    [InlineData(SemanticConventionVersion.V1_45_0, "messaging.system.name", "aws.sqs", ActivityKind.Producer)]
    [InlineData(SemanticConventionVersion.Latest, "messaging.system.name", "aws.sqs", ActivityKind.Producer)]
#if NETFRAMEWORK
    public void TestSQSSendMessageForSemanticConventionVersion(SemanticConventionVersion version, string? expectedKey, string? expectedValue, ActivityKind expectedKind)
#else
    public async Task TestSQSSendMessageForSemanticConventionVersion(SemanticConventionVersion version, string? expectedKey, string? expectedValue, ActivityKind expectedKind)
#endif
    {
        var exportedItems = new List<Activity>();
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(sqs, """{ "MessageId": "567910cd-659e-55d4-bc19-f29d9g3b2378" }""", requestId, extendedRequestId, true);
            var request = new SendMessageRequest
            {
                QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                MessageBody = "Hello from OT",
            };
#if NETFRAMEWORK
            sqs.SendMessage(request);
#else
            await sqs.SendMessageAsync(request, TestContext.Current.CancellationToken);
#endif
        }

        var activity = Assert.Single(exportedItems, e => e.DisplayName == "SQS.SendMessage");

        // Sending a single message is a producer span from v1.44.0 of the semantic conventions onwards.
        Assert.Equal(expectedKind, activity.Kind);

        // The messaging attributes are only emitted from 1.40.0 onwards, and only the name
        // defined by the selected version of the semantic conventions should be emitted.
        Assert.Equal(expectedKey == "messaging.system" ? expectedValue : null, Utils.GetTagValue(activity, "messaging.system"));
        Assert.Equal(expectedKey == "messaging.system.name" ? expectedValue : null, Utils.GetTagValue(activity, "messaging.system.name"));

        // The batch message count must not be set for operations on a single message.
        Assert.Null(Utils.GetTagValue(activity, "messaging.batch.message_count"));
    }

    [Theory]
    [InlineData(SemanticConventionVersion.V1_28_0, null)]
    [InlineData(SemanticConventionVersion.V1_29_0, null)]
    [InlineData(SemanticConventionVersion.V1_40_0, null)]
    [InlineData(SemanticConventionVersion.V1_45_0, 2)]
    [InlineData(SemanticConventionVersion.Latest, 2)]
#if NETFRAMEWORK
    public void TestSQSSendMessageBatchForSemanticConventionVersion(SemanticConventionVersion version, int? expectedMessageCount)
#else
    public async Task TestSQSSendMessageBatchForSemanticConventionVersion(SemanticConventionVersion version, int? expectedMessageCount)
#endif
    {
        var exportedItems = new List<Activity>();
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(sqs, """{ "Successful": [], "Failed": [] }""", requestId, extendedRequestId, true);
            var request = new SendMessageBatchRequest
            {
                QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                Entries =
                [
                    new SendMessageBatchRequestEntry { Id = "1", MessageBody = "Hello" },
                    new SendMessageBatchRequestEntry { Id = "2", MessageBody = "from OT" },
                ],
            };
#if NETFRAMEWORK
            sqs.SendMessageBatch(request);
#else
            await sqs.SendMessageBatchAsync(request, TestContext.Current.CancellationToken);
#endif
        }

        var activity = Assert.Single(exportedItems, e => e.DisplayName == "SQS.SendMessageBatch");

        // The context is not injected into the messages of a batch so these spans are not producer spans.
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(expectedMessageCount, Utils.GetTagValue(activity, "messaging.batch.message_count"));

        if (expectedMessageCount is not null)
        {
            Assert.Equal("SendMessageBatch", Utils.GetTagValue(activity, "messaging.operation.name"));
            Assert.Equal("send", Utils.GetTagValue(activity, "messaging.operation.type"));
            Assert.Equal("MyTestQueue", Utils.GetTagValue(activity, "messaging.destination.name"));
        }
    }

    [Theory]
    [InlineData(SemanticConventionVersion.V1_28_0, 2, null)]
    [InlineData(SemanticConventionVersion.V1_29_0, 2, null)]
    [InlineData(SemanticConventionVersion.V1_40_0, 2, null)]
    [InlineData(SemanticConventionVersion.V1_45_0, 2, 2)]
    [InlineData(SemanticConventionVersion.V1_45_0, 1, 1)]
    [InlineData(SemanticConventionVersion.V1_45_0, 0, 0)]
    [InlineData(SemanticConventionVersion.Latest, 2, 2)]
#if NETFRAMEWORK
    public void TestSQSReceiveMessageForSemanticConventionVersion(SemanticConventionVersion version, int receivedMessages, int? expectedMessageCount)
#else
    public async Task TestSQSReceiveMessageForSemanticConventionVersion(SemanticConventionVersion version, int receivedMessages, int? expectedMessageCount)
#endif
    {
        var exportedItems = new List<Activity>();
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

        var messages = string.Join(
            ",",
            Enumerable.Range(1, receivedMessages).Select(i => $$"""{ "MessageId": "message-{{i}}", "ReceiptHandle": "receipt-{{i}}", "Body": "Hello from OT", "MD5OfBody": "31e2ad304c624ee219a98386463498d9" }"""));

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(sqs, $$"""{ "Messages": [{{messages}}] }""", requestId, extendedRequestId, true);
            var request = new ReceiveMessageRequest
            {
                QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                MaxNumberOfMessages = 10,
            };
#if NETFRAMEWORK
            sqs.ReceiveMessage(request);
#else
            await sqs.ReceiveMessageAsync(request, TestContext.Current.CancellationToken);
#endif
        }

        var activity = Assert.Single(exportedItems, e => e.DisplayName == "SQS.ReceiveMessage");

        // Receiving messages remains a client span.
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(expectedMessageCount, Utils.GetTagValue(activity, "messaging.batch.message_count"));

        if (expectedMessageCount is not null)
        {
            Assert.Equal("ReceiveMessage", Utils.GetTagValue(activity, "messaging.operation.name"));
            Assert.Equal("receive", Utils.GetTagValue(activity, "messaging.operation.type"));
            Assert.Equal("MyTestQueue", Utils.GetTagValue(activity, "messaging.destination.name"));
        }
    }

    [Theory]
    [InlineData(SemanticConventionVersion.V1_28_0, null)]
    [InlineData(SemanticConventionVersion.V1_29_0, null)]
    [InlineData(SemanticConventionVersion.V1_40_0, null)]
    [InlineData(SemanticConventionVersion.V1_45_0, 3)]
    [InlineData(SemanticConventionVersion.Latest, 3)]
#if NETFRAMEWORK
    public void TestSNSPublishBatchForSemanticConventionVersion(SemanticConventionVersion version, int? expectedMessageCount)
#else
    public async Task TestSNSPublishBatchForSemanticConventionVersion(SemanticConventionVersion version, int? expectedMessageCount)
#endif
    {
        var exportedItems = new List<Activity>();
        var requestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var extendedRequestId = "wzHcyEWfmOGDIE5QOhTAqFDoDWP3y8IUvpNINCwL9N4TEHbUw0/gZJ+VZTmCNCWR7fezEN3eCiQ=";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sns = new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = """
                <PublishBatchResponse xmlns="https://sns.amazonaws.com/doc/2010-03-31/">
                  <PublishBatchResult>
                    <Successful />
                    <Failed />
                  </PublishBatchResult>
                  <ResponseMetadata>
                    <RequestId>fakerequ-esti-dfak-ereq-uestidfakere</RequestId>
                  </ResponseMetadata>
                </PublishBatchResponse>
                """;
            CustomResponses.SetResponse(sns, dummyResponse, requestId, extendedRequestId, true);
            var request = new Amazon.SimpleNotificationService.Model.PublishBatchRequest
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789:MyTestTopic",
                PublishBatchRequestEntries =
                [
                    new Amazon.SimpleNotificationService.Model.PublishBatchRequestEntry { Id = "1", Message = "Hello" },
                    new Amazon.SimpleNotificationService.Model.PublishBatchRequestEntry { Id = "2", Message = "from" },
                    new Amazon.SimpleNotificationService.Model.PublishBatchRequestEntry { Id = "3", Message = "OT" },
                ],
            };
#if NETFRAMEWORK
            sns.PublishBatch(request);
#else
            await sns.PublishBatchAsync(request, TestContext.Current.CancellationToken);
#endif
        }

        var activity = Assert.Single(exportedItems, e => e.DisplayName == "SNS.PublishBatch");

        // The context is not injected into the messages of a batch so these spans are not producer spans.
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(expectedMessageCount, Utils.GetTagValue(activity, "messaging.batch.message_count"));

        if (expectedMessageCount is not null)
        {
            Assert.Equal("PublishBatch", Utils.GetTagValue(activity, "messaging.operation.name"));
            Assert.Equal("send", Utils.GetTagValue(activity, "messaging.operation.type"));
            Assert.Equal("MyTestTopic", Utils.GetTagValue(activity, "messaging.destination.name"));
        }
    }

    [Theory]
    [InlineData(true, SemanticConventionVersion.V1_40_0)]
    [InlineData(false, SemanticConventionVersion.V1_40_0)]
    [InlineData(true, SemanticConventionVersion.V1_45_0)]
    [InlineData(false, SemanticConventionVersion.V1_45_0)]
    [InlineData(true, SemanticConventionVersion.Latest)]
    [InlineData(false, SemanticConventionVersion.Latest)]
#if NETFRAMEWORK
    public void TestMessageSendWithCreationContext(bool isSqs, SemanticConventionVersion version)
#else
    public async Task TestMessageSendWithCreationContext(bool isSqs, SemanticConventionVersion version)
#endif
    {
        const string TraceParent = "00-5759e988bd862e3fe1be46a994272793-53995c3f42cd8ad8-01";
        const string TraceState = "key=value";
        const string RequestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var exportedItems = new List<Activity>();
        var sampledKinds = new List<ActivityKind>();
        using var parent = new Activity("parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.TelemetryScopePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                if (options.Name == (isSqs ? "SQS.SendMessage" : "SNS.Publish"))
                {
                    sampledKinds.Add(options.Kind);
                }

                return ActivitySamplingResult.None;
            },
        };
        ActivitySource.AddActivityListener(listener);

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            if (isSqs)
            {
                using var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
                CustomResponses.SetResponse(sqs, """{ "MessageId": "567910cd-659e-55d4-bc19-f29d9g3b2378" }""", RequestId, "extended-request-id", true);
                var request = new SendMessageRequest
                {
                    QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                    MessageBody = "Hello from OT",
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["traceparent"] = new() { DataType = "String", StringValue = TraceParent },
                        ["tracestate"] = new() { DataType = "String", StringValue = TraceState },
                    },
                };
#if NETFRAMEWORK
                sqs.SendMessage(request);
#else
                await sqs.SendMessageAsync(request, TestContext.Current.CancellationToken);
#endif
                Assert.Equal(TraceParent, request.MessageAttributes["traceparent"].StringValue);
                Assert.Equal(TraceState, request.MessageAttributes["tracestate"].StringValue);
            }
            else
            {
                using var sns = new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
                CustomResponses.SetResponse(
                    sns,
                    """
                    <PublishResponse xmlns="https://sns.amazonaws.com/doc/2010-03-31/">
                      <PublishResult><MessageId>567910cd-659e-55d4-bc19-f29d9g3b2378</MessageId></PublishResult>
                      <ResponseMetadata><RequestId>fakerequ-esti-dfak-ereq-uestidfakere</RequestId></ResponseMetadata>
                    </PublishResponse>
                    """,
                    RequestId,
                    "extended-request-id",
                    true);
                var request = new Amazon.SimpleNotificationService.Model.PublishRequest
                {
                    TopicArn = "arn:aws:sns:us-east-1:123456789:MyTestTopic",
                    Message = "Hello from OT",
                    MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
                    {
                        ["traceparent"] = new() { DataType = "String", StringValue = TraceParent },
                        ["tracestate"] = new() { DataType = "String", StringValue = TraceState },
                    },
                };
#if NETFRAMEWORK
                sns.Publish(request);
#else
                await sns.PublishAsync(request, TestContext.Current.CancellationToken);
#endif
                Assert.Equal(TraceParent, request.MessageAttributes["traceparent"].StringValue);
                Assert.Equal(TraceState, request.MessageAttributes["tracestate"].StringValue);
            }

            Assert.Same(parent, Activity.Current);
        }

        var activity = Assert.Single(exportedItems, item => item.DisplayName == (isSqs ? "SQS.SendMessage" : "SNS.Publish"));
        this.ValidateAWSActivity(activity, parent);
        Assert.Equal(ActivityKind.Client, Assert.Single(sampledKinds));

        if (version == SemanticConventionVersion.V1_40_0)
        {
            Assert.Empty(activity.Links);
        }
        else
        {
            Assert.True(ActivityContext.TryParse(TraceParent, TraceState, isRemote: true, out var creationContext));
            Assert.Equal(creationContext, Assert.Single(activity.Links).Context);
        }
    }

    [Theory]
    [InlineData(true, SemanticConventionVersion.V1_45_0)]
    [InlineData(false, SemanticConventionVersion.V1_45_0)]
    [InlineData(true, SemanticConventionVersion.Latest)]
    [InlineData(false, SemanticConventionVersion.Latest)]
#if NETFRAMEWORK
    public void TestMessageSendReusedRequestIsProducerEachTime(bool isSqs, SemanticConventionVersion version)
#else
    public async Task TestMessageSendReusedRequestIsProducerEachTime(bool isSqs, SemanticConventionVersion version)
#endif
    {
        const string RequestId = "fakerequ-esti-dfak-ereq-uestidfakere";
        var exportedItems = new List<Activity>();
        string? lastInjectedTraceParent = null;

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            if (isSqs)
            {
                using var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
                var request = new SendMessageRequest
                {
                    QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
                    MessageBody = "Hello from OT",
                };

                for (var i = 0; i < 2; i++)
                {
                    CustomResponses.SetResponse(sqs, """{ "MessageId": "567910cd-659e-55d4-bc19-f29d9g3b2378" }""", RequestId, "extended-request-id", true);
#if NETFRAMEWORK
                    sqs.SendMessage(request);
#else
                    await sqs.SendMessageAsync(request, TestContext.Current.CancellationToken);
#endif
                }

                lastInjectedTraceParent = request.MessageAttributes["traceparent"].StringValue;
            }
            else
            {
                using var sns = new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
                var request = new Amazon.SimpleNotificationService.Model.PublishRequest
                {
                    TopicArn = "arn:aws:sns:us-east-1:123456789:MyTestTopic",
                    Message = "Hello from OT",
                };

                for (var i = 0; i < 2; i++)
                {
                    CustomResponses.SetResponse(
                        sns,
                        """
                        <PublishResponse xmlns="https://sns.amazonaws.com/doc/2010-03-31/">
                          <PublishResult><MessageId>567910cd-659e-55d4-bc19-f29d9g3b2378</MessageId></PublishResult>
                          <ResponseMetadata><RequestId>fakerequ-esti-dfak-ereq-uestidfakere</RequestId></ResponseMetadata>
                        </PublishResponse>
                        """,
                        RequestId,
                        "extended-request-id",
                        true);
#if NETFRAMEWORK
                    sns.Publish(request);
#else
                    await sns.PublishAsync(request, TestContext.Current.CancellationToken);
#endif
                }

                lastInjectedTraceParent = request.MessageAttributes["traceparent"].StringValue;
            }
        }

        // The context injected by the first send is the instrumentation's own, not a creation context
        // supplied by the application, so the second send must not be a client span linked to the first.
        var sends = exportedItems.Where(a => a.DisplayName == (isSqs ? "SQS.SendMessage" : "SNS.Publish")).ToList();
        Assert.Equal(2, sends.Count);
        Assert.All(sends, activity =>
        {
            Assert.Equal(ActivityKind.Producer, activity.Kind);
            Assert.Empty(activity.Links);
        });

        // The context of the last send replaces the one injected by the first.
        Assert.Contains(sends[1].SpanId.ToHexString(), lastInjectedTraceParent, StringComparison.Ordinal);
        Assert.DoesNotContain(sends[0].SpanId.ToHexString(), lastInjectedTraceParent, StringComparison.Ordinal);
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSQSSendMessageReusedRequestWithoutActivity_ContextNotResent()
#else
    public async Task TestSQSSendMessageReusedRequestWithoutActivity_ContextNotResent()
#endif
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSInstrumentation()
            .Build();

        var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
        CustomResponses.SetResponse(sqs, """{ "MessageId": "567910cd-659e-55d4-bc19-f29d9g3b2378" }""", "fakerequ-esti-dfak-ereq-uestidfakere", "extended", true);

        var request = new SendMessageRequest
        {
            QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue",
            MessageBody = "Hello from OT",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["Custom"] = new() { StringValue = "Value", DataType = "String" },
            },
        };

#if NETFRAMEWORK
        sqs.SendMessage(request);
#else
        await sqs.SendMessageAsync(request, TestContext.Current.CancellationToken);
#endif

        Assert.Contains("traceparent", request.MessageAttributes.Keys);

        // The client keeps its instrumented pipeline, but there is no longer any current activity.
        tracerProvider.Dispose();
        Assert.Null(Activity.Current);

#if NETFRAMEWORK
        sqs.SendMessage(request);
#else
        await sqs.SendMessageAsync(request, TestContext.Current.CancellationToken);
#endif

        Assert.Single(request.MessageAttributes);
        Assert.Contains("Custom", request.MessageAttributes.Keys);
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSNSPublishReusedRequestWithoutActivity_ContextNotResent()
#else
    public async Task TestSNSPublishReusedRequestWithoutActivity_ContextNotResent()
#endif
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSInstrumentation()
            .Build();

        var sns = new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
        CustomResponses.SetResponse(
            sns,
            """
            <PublishResponse xmlns="https://sns.amazonaws.com/doc/2010-03-31/">
              <PublishResult>
                <MessageId>567910cd-659e-55d4-bc19-f29d9g3b2378</MessageId>
              </PublishResult>
              <ResponseMetadata>
                <RequestId>fakerequ-esti-dfak-ereq-uestidfakere</RequestId>
              </ResponseMetadata>
            </PublishResponse>
            """,
            "fakerequ-esti-dfak-ereq-uestidfakere",
            "extended",
            true);

        var request = new Amazon.SimpleNotificationService.Model.PublishRequest
        {
            TopicArn = "arn:aws:sns:us-east-1:123456789:MyTestTopic",
            Message = "Hello from OT",
            MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
            {
                ["Custom"] = new() { StringValue = "Value", DataType = "String" },
            },
        };

#if NETFRAMEWORK
        sns.Publish(request);
#else
        await sns.PublishAsync(request, TestContext.Current.CancellationToken);
#endif

        Assert.Contains("traceparent", request.MessageAttributes.Keys);

        // The client keeps its instrumented pipeline, but there is no longer any current activity.
        tracerProvider.Dispose();
        Assert.Null(Activity.Current);

#if NETFRAMEWORK
        sns.Publish(request);
#else
        await sns.PublishAsync(request, TestContext.Current.CancellationToken);
#endif

        Assert.Single(request.MessageAttributes);
        Assert.Contains("Custom", request.MessageAttributes.Keys);
    }

    [Fact]
    public async Task SuppressDownstreamInstrumentation_RestoresSuppressionScope()
    {
        Assert.False(Sdk.SuppressInstrumentation);

        var previousActivity = Activity.Current;
        Activity.Current = null;

        try
        {
            var handler = new AWSTracingPipelineHandler(
                new AWSClientInstrumentationOptions { SuppressDownstreamInstrumentation = true })
            {
                InnerHandler = new SuppressionAssertPipelineHandler(),
            };

            var executionContext = new Amazon.Runtime.Internal.ExecutionContext(
                new TestRequestContext(new SendMessageRequest(), new TestRequest()),
                null);

            handler.InvokeSync(executionContext);
            Assert.False(Sdk.SuppressInstrumentation);

            await handler.InvokeAsync<AmazonWebServiceResponse>(executionContext);
            Assert.False(Sdk.SuppressInstrumentation);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    public void Dispose() =>
        RuntimePipelineCustomizerRegistry.Instance.Deregister(AWS.Implementation.AWSTracingPipelineCustomizer.UniqueName);

    private void ValidateAWSActivity(Activity aws_activity, Activity parent, ActivityKind expectedKind = ActivityKind.Client)
    {
        Assert.Equal(parent.SpanId, aws_activity.ParentSpanId);
        Assert.Equal(expectedKind, aws_activity.Kind);
        Assert.NotNull(aws_activity.Source.Version);
        Assert.NotEmpty(aws_activity.Source.Version);
        Assert.StartsWith("https://opentelemetry.io/schemas/", aws_activity.Source.TelemetrySchemaUrl);
    }

    private void ValidateDynamoActivityTags(Activity ddb_activity)
    {
        Assert.Equal("DynamoDB.Scan", ddb_activity.DisplayName);
        Assert.Equal(ExpectedDynamoTableNames, Utils.GetTagValue(ddb_activity, "aws.dynamodb.table_names"));
        Assert.Equal("dynamodb", Utils.GetTagValue(ddb_activity, "db.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(ddb_activity, "rpc.system.name"));
        Assert.Equal("DynamoDB", Utils.GetTagValue(ddb_activity, "rpc.service"));
        Assert.Equal("Scan", Utils.GetTagValue(ddb_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(ddb_activity, "cloud.region"));
    }

    private void ValidateSqsActivityTags(Activity sqs_activity)
    {
        Assert.Equal("SQS.SendMessage", sqs_activity.DisplayName);
        Assert.Equal("https://sqs.us-east-1.amazonaws.com/123456789/MyTestQueue", Utils.GetTagValue(sqs_activity, "aws.sqs.queue.url"));
        Assert.Equal("aws-api", Utils.GetTagValue(sqs_activity, "rpc.system.name"));
        Assert.Equal("SQS", Utils.GetTagValue(sqs_activity, "rpc.service"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(sqs_activity, "cloud.region"));
        Assert.Equal("MyTestQueue", Utils.GetTagValue(sqs_activity, "messaging.destination.name"));
        Assert.Equal("567910cd-659e-55d4-bc19-f29d9g3b2378", Utils.GetTagValue(sqs_activity, "messaging.message.id"));
        Assert.Equal("SendMessage", Utils.GetTagValue(sqs_activity, "messaging.operation.name"));
        Assert.Equal("send", Utils.GetTagValue(sqs_activity, "messaging.operation.type"));
        Assert.Equal("sqs.us-east-1.amazonaws.com", Utils.GetTagValue(sqs_activity, "server.address"));
        Assert.Equal("aws.sqs", Utils.GetTagValue(sqs_activity, "messaging.system.name"));
    }

    private void ValidateSnsActivityTags(Activity sns_activity)
    {
        Assert.Equal("SNS.Publish", sns_activity.DisplayName);
        Assert.Equal("arn:aws:sns:us-east-1:123456789:MyTestTopic", Utils.GetTagValue(sns_activity, "aws.sns.topic.arn"));
        Assert.Equal("aws-api", Utils.GetTagValue(sns_activity, "rpc.system.name"));
        Assert.Equal("SNS", Utils.GetTagValue(sns_activity, "rpc.service"));
        Assert.Equal("Publish", Utils.GetTagValue(sns_activity, "rpc.method"));
        Assert.Equal("MyTestTopic", Utils.GetTagValue(sns_activity, "messaging.destination.name"));
        Assert.Equal("567910cd-659e-55d4-bc19-f29d9g3b2378", Utils.GetTagValue(sns_activity, "messaging.message.id"));
        Assert.Equal("Publish", Utils.GetTagValue(sns_activity, "messaging.operation.name"));
        Assert.Equal("send", Utils.GetTagValue(sns_activity, "messaging.operation.type"));
        Assert.Equal("aws.sns", Utils.GetTagValue(sns_activity, "messaging.system.name"));
    }

    private void ValidateBedrockActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock.GetGuardrail", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.guardrail.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
        Assert.Equal("Bedrock", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetGuardrail", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockRuntimeActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Runtime.InvokeModel", bedrock_activity.DisplayName);
        Assert.Equal("amazon.titan-text-express-v1", Utils.GetTagValue(bedrock_activity, "gen_ai.request.model"));
        Assert.Equal("aws.bedrock", Utils.GetTagValue(bedrock_activity, "gen_ai.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
        Assert.Equal("Bedrock Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeModel", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetAgent", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetKnowledgeBase", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetKnowledgeBase", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentDataSourceOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetDataSource", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.data_source.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetDataSource", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentRuntimeAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.InvokeAgent", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "cloud.region"));
    }

    private void ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.Retrieve", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system.name"));
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

    private sealed class SuppressionAssertPipelineHandler : PipelineHandler
    {
        public override void InvokeSync(IExecutionContext executionContext) =>
            Assert.True(Sdk.SuppressInstrumentation);

        public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
        {
            Assert.True(Sdk.SuppressInstrumentation);
            return Task.FromResult(default(T)!);
        }
    }
}
