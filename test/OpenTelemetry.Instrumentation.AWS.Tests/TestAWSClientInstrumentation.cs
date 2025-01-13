// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
    private static readonly string[] BedrockRuntimeExpectedFinishReasons = ["finish_reason"];

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
            CustomResponses.SetResponse(ddb, null, requestId, true);
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
            CustomResponses.SetResponse(ddb, null, requestId, true);
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
    public void TestBedrockRuntimeInvokeModelNovaSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelNovaSuccessful()
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
            var dummyResponse = @"
            {
                ""usage"":
                {
                    ""inputTokens"": 12345,
                    ""outputTokens"": 67890
                },
                ""stopReason"": ""finish_reason""
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "amazon.nova-micro-v1:0",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    inferenceConfig = new
                    {
                        temperature = 0.123,
                        top_p = 0.456,
                        max_new_tokens = 789,
                    },
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "amazon.nova-micro-v1:0");

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelTitanSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelTitanSuccessful()
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
            var dummyResponse = @"
            {
                ""inputTextTokenCount"": 12345,
                ""results"": [
                    {
                        ""tokenCount"": 67890,
                        ""completionReason"": ""finish_reason""
                    }
                ]
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "amazon.titan-text-express-v1",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    textGenerationConfig = new
                    {
                        temperature = 0.123,
                        topP = 0.456,
                        maxTokenCount = 789,
                    },
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "amazon.titan-text-express-v1");

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelClaudeSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelClaudeSuccessful()
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
            var dummyResponse = @"
            {
                ""usage"":
                {
                    ""input_tokens"": 12345,
                    ""output_tokens"": 67890
                },
                ""stop_reason"": ""finish_reason""
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "anthropic.claude-3-5-haiku-202410-22-v1:0",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    temperature = 0.123,
                    top_p = 0.456,
                    max_tokens = 789,
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "anthropic.claude-3-5-haiku-202410-22-v1:0");

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelLlamaSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelLlamaSuccessful()
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
            var dummyResponse = @"
            {
                ""prompt_token_count"": 12345,
                ""generation_token_count"": 67890,
                ""stop_reason"": ""finish_reason""
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "meta.llama3-8b-instruct-v1:0",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    temperature = 0.123,
                    top_p = 0.456,
                    max_gen_len = 789,
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "meta.llama3-8b-instruct-v1:0");

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelCommandSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelCommandSuccessful()
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

            // no input_tokens or output_tokens in response body, so we generate input and output text of the desired length
            // (6 chars * number of tokens) to get the desired token estimation.
            var dummyResponse = @"
            {
                ""text"": """ + string.Concat(Enumerable.Repeat("sample", 67890)) + @""",
                ""finish_reason"": ""finish_reason""
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "cohere.command-r-v1:0",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    message = string.Concat(Enumerable.Repeat("sample", 12345)),
                    temperature = 0.123,
                    p = 0.456,
                    max_tokens = 789,
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "cohere.command-r-v1:0");

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelJambaSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelJambaSuccessful()
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
            var dummyResponse = @"
            {
                ""usage"":
                {
                    ""prompt_tokens"": 12345,
                    ""completion_tokens"": 67890
                },
                ""choices"": [
                    {
                        ""finish_reason"": ""finish_reason""
                    }
                ]
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "ai21.jamba-1-5-large-v1:0",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    temperature = 0.123,
                    top_p = 0.456,
                    max_tokens = 789,
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "ai21.jamba-1-5-large-v1:0");

        Assert.Equal(ActivityStatusCode.Unset, awssdk_activity.Status);
        Assert.Equal(requestId, Utils.GetTagValue(awssdk_activity, "aws.request_id"));
    }

    [Fact]
#if NETFRAMEWORK
    public void TestBedrockRuntimeInvokeModelMistralSuccessful()
#else
    public async Task TestBedrockRuntimeInvokeModelMistralSuccessful()
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

            // no input_tokens or output_tokens in response body, so we generate input and output text of the desired length
            // (6 chars * number of tokens) to get the desired token estimation.
            var dummyResponse = @"
            {
                ""outputs"": [
                    {
                        ""text"": """ + string.Concat(Enumerable.Repeat("sample", 67890)) + @""",
                        ""stop_reason"": ""finish_reason""
                    }
                ]
            }";
            CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
            var invokeModelRequest = new InvokeModelRequest
            {
                ModelId = "mistral.mistral-7b-instruct-v0:2",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    prompt = string.Concat(Enumerable.Repeat("sample", 12345)),
                    temperature = 0.123,
                    top_p = 0.456,
                    max_tokens = 789,
                }))),
            };
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
        this.ValidateBedrockRuntimeActivityTags(awssdk_activity, "mistral.mistral-7b-instruct-v0:2");

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

    private void ValidateAWSActivity(Activity aws_activity, Activity parent)
    {
        Assert.Equal(parent.SpanId, aws_activity.ParentSpanId);
        Assert.Equal(ActivityKind.Client, aws_activity.Kind);
    }

    private void ValidateDynamoActivityTags(Activity ddb_activity)
    {
        Assert.Equal("DynamoDB.Scan", ddb_activity.DisplayName);
        Assert.Equal("SampleProduct", Utils.GetTagValue(ddb_activity, "aws.dynamodb.table_names"));
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
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.guardrail.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetGuardrail", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockRuntimeActivityTags(Activity bedrock_activity, string model_id)
    {
        Assert.Equal("Bedrock Runtime.InvokeModel", bedrock_activity.DisplayName);
        Assert.Equal(model_id, Utils.GetTagValue(bedrock_activity, "gen_ai.request.model"));
        Assert.Equal("aws.bedrock", Utils.GetTagValue(bedrock_activity, "gen_ai.system"));
        Assert.Equal(0.123, Utils.GetTagValue(bedrock_activity, "gen_ai.request.temperature"));
        Assert.Equal(0.456, Utils.GetTagValue(bedrock_activity, "gen_ai.request.top_p"));
        Assert.Equal(789, Utils.GetTagValue(bedrock_activity, "gen_ai.request.max_tokens"));
        Assert.Equal(12345, Utils.GetTagValue(bedrock_activity, "gen_ai.usage.input_tokens"));
        Assert.Equal(67890, Utils.GetTagValue(bedrock_activity, "gen_ai.usage.output_tokens"));
        Assert.Equal(BedrockRuntimeExpectedFinishReasons, Utils.GetTagValue(bedrock_activity, "gen_ai.response.finish_reasons"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeModel", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetAgent", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetKnowledgeBase", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetKnowledgeBase", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentDataSourceOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent.GetDataSource", bedrock_activity.DisplayName);
        Assert.Equal("1234567890", Utils.GetTagValue(bedrock_activity, "aws.bedrock.data_source.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("GetDataSource", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentRuntimeAgentOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.InvokeAgent", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.agent.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("InvokeAgent", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }

    private void ValidateBedrockAgentRuntimeKnowledgeBaseOpsActivityTags(Activity bedrock_activity)
    {
        Assert.Equal("Bedrock Agent Runtime.Retrieve", bedrock_activity.DisplayName);
        Assert.Equal("123456789", Utils.GetTagValue(bedrock_activity, "aws.bedrock.knowledge_base.id"));
        Assert.Equal("aws-api", Utils.GetTagValue(bedrock_activity, "rpc.system"));
        Assert.Equal("Bedrock Agent Runtime", Utils.GetTagValue(bedrock_activity, "rpc.service"));
        Assert.Equal("Retrieve", Utils.GetTagValue(bedrock_activity, "rpc.method"));
    }
}
