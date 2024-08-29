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
        string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
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
        }

        Assert.NotEmpty(exportedItems);

        Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
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
        string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new TestAmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
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
        }

        Assert.NotEmpty(exportedItems);

        Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
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
        string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddXRayTraceId()
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
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
            catch (AmazonServiceException ex)
            {
                Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        Assert.NotEmpty(exportedItems);

        Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "DynamoDB.Scan");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateDynamoActivityTags(awssdk_activity);

        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
        Assert.Equal(Status.Error.WithDescription("Exception of type 'Amazon.Runtime.AmazonServiceException' was thrown."), awssdk_activity.GetStatus());
        Assert.Equal("exception", awssdk_activity.Events.First().Name);
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
        string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
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
        }

        Assert.NotEmpty(exportedItems);
        Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "SQS.SendMessage");
        Assert.NotNull(awssdk_activity);

        this.ValidateAWSActivity(awssdk_activity, parent);
        this.ValidateSqsActivityTags(awssdk_activity);

        Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
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

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrock = new AmazonBedrockClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{\"GuardrailId\":\"123456789\"}";
            CustomResponses.SetResponse(bedrock, dummyResponse, requestId, true);
            var get_guardrail_req = new GetGuardrailRequest();
            get_guardrail_req.GuardrailIdentifier = "123456789";
#if NETFRAMEWORK
            var response = bedrock.GetGuardrail(get_guardrail_req);
#else
            var response = await bedrock.GetGuardrailAsync(get_guardrail_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock.GetGuardrail");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
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

        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceId()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var bedrockruntime = new AmazonBedrockRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invoke_model_req = new InvokeModelRequest();
            invoke_model_req.ModelId = "amazon.titan-text-express-v1";
#if NETFRAMEWORK
            var response = bedrockruntime.InvokeModel(invoke_model_req);
#else
            var response = await bedrockruntime.InvokeModelAsync(invoke_model_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Runtime.InvokeModel");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockRuntimeActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

// Agent related operation for Bedrock Agent
    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentGetAgentSuccessful()
#else
    public async Task TestBedrockAgentGetAgentSuccessful()
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
            var bedrockagent = new AmazonBedrockAgentClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, true);
            var get_agent_req = new GetAgentRequest();
            get_agent_req.AgentId = "1234567890";
#if NETFRAMEWORK
            var response = bedrockagent.GetAgent(get_agent_req);
#else
            var response = await bedrockagent.GetAgentAsync(get_agent_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetAgent");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockAgentAgentOpsActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

// Knowledge Base related operation for Bedrock Agent
    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentGetKnowledgeBaseSuccessful()
#else
    public async Task TestBedrockAgentGetKnowledgeBaseSuccessful()
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
            var bedrockagent = new AmazonBedrockAgentClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, true);
            var get_knowledge_base_req = new GetKnowledgeBaseRequest();
            get_knowledge_base_req.KnowledgeBaseId = "1234567890";
#if NETFRAMEWORK
            var response = bedrockagent.GetKnowledgeBase(get_knowledge_base_req);
#else
            var response = await bedrockagent.GetKnowledgeBaseAsync(get_knowledge_base_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetKnowledgeBase");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockAgentKnowledgeBaseOpsActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

// Data Source related operation for Bedrock Agent
    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentGetDataSourceSuccessful()
#else
    public async Task TestBedrockAgentGetDataSourceSuccessful()
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
            var bedrockagent = new AmazonBedrockAgentClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagent, dummyResponse, requestId, true);
            var get_data_source_req = new GetDataSourceRequest();
            get_data_source_req.DataSourceId = "1234567890";
            get_data_source_req.KnowledgeBaseId = "1234567890";
#if NETFRAMEWORK
            var response = bedrockagent.GetDataSource(get_data_source_req);
#else
            var response = await bedrockagent.GetDataSourceAsync(get_data_source_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent.GetDataSource");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockAgentDataSourceOpsActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

// Agent related operation for Bedrock Agent Runtime
    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentRuntimeInvokeAgentSuccessful()
#else
    public async Task TestBedrockAgentRuntimeInvokeAgentSuccessful()
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
            var bedrockagentruntime = new AmazonBedrockAgentRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagentruntime, dummyResponse, requestId, true);
            var invoke_agent_req = new InvokeAgentRequest();
            invoke_agent_req.AgentId = "123456789";
            invoke_agent_req.AgentAliasId = "testalias";
            invoke_agent_req.SessionId = "test-session-id";
            invoke_agent_req.InputText = "sample input text";
#if NETFRAMEWORK
            var response = bedrockagentruntime.InvokeAgent(invoke_agent_req);
#else
            var response = await bedrockagentruntime.InvokeAgentAsync(invoke_agent_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent Runtime.InvokeAgent");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockAgentRuntimeAgentOpsActivityTags(awssdk_activity);

            Assert.Equal(Status.Unset, awssdk_activity.GetStatus());
            Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.requestId"));
        }
    }

// Knowledge Base related operation for Bedrock Agent Runtime
    [Fact]
#if NETFRAMEWORK
    public void TestBedrockAgentRuntimeRetrieveSuccessful()
#else
    public async Task TestBedrockAgentRuntimeRetrieveSuccessful()
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
            var bedrockagentruntime = new AmazonBedrockAgentRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            string requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";
            string dummyResponse = "{}";
            CustomResponses.SetResponse(bedrockagentruntime, dummyResponse, requestId, true);
            var retrieve_req = new RetrieveRequest();
            retrieve_req.KnowledgeBaseId = "123456789";
#if NETFRAMEWORK
            var response = bedrockagentruntime.Retrieve(retrieve_req);
#else
            var response = await bedrockagentruntime.RetrieveAsync(retrieve_req);
#endif
            Assert.NotEmpty(exportedItems);
            Activity? awssdk_activity = exportedItems.FirstOrDefault(e => e.DisplayName == "Bedrock Agent Runtime.Retrieve");
            Assert.NotNull(awssdk_activity);

            this.ValidateAWSActivity(awssdk_activity, parent);
            this.ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(awssdk_activity);

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

    private void ValidateBedrockActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock.GetGuardrail", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("GetGuardrail", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.guardrail.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetGuardrail", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockRuntimeActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Runtime.InvokeModel", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock Runtime", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("InvokeModel", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("amazon.titan-text-express-v1", Utils.GetTagValue(bedrock_activity, "gen_ai.request.model"));
        Assert.Equal("aws_bedrock", Utils.GetTagValue(bedrock_activity, "gen_ai.system"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeModel", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetAgent", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("GetAgent", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetKnowledgeBase", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("GetKnowledgeBase", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetKnowledgeBase", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentDataSourceOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetDataSource", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("GetDataSource", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.data_source.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetDataSource", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentRuntimeAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.InvokeAgent", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("InvokeAgent", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.Retrieve", bedrock_activity.DisplayName);
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "aws.service"));
        Assert.Equal("Retrieve", Utils.GetTagValue(bedrock_activity, "aws.operation"));
        Assert.Equal("us-east-1", Utils.GetTagValue(bedrock_activity, "aws.region"));
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("Retrieve", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }
}
