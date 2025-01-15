// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry.AWS;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSLlmModelProcessor
{
#if NET
    [UnconditionalSuppressMessage(
        "Specify StringComparison for clarity",
        "CA1307",
        Justification = "Adding StringComparison only works for NET Core but not the framework.")]
#endif
    internal static void ProcessGenAiAttributes(Activity activity, MemoryStream body, string modelName, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        // message can be either a request or a response. isRequest is used by the model-specific methods to determine
        // whether to extract the request or response attributes.

        // Currently, the .NET SDK does not expose "X-Amzn-Bedrock-*" HTTP headers in the response metadata, as per
        // https://github.com/aws/aws-sdk-net/issues/3171. As a result, we can only extract attributes given what is in
        // the response body. For the Claude, Command, and Mistral models, the input and output tokens are not provided
        // in the response body, so we approximate their values by dividing the input and output lengths by 6, based on
        // the Bedrock documentation here: https://docs.aws.amazon.com/bedrock/latest/userguide/model-customization-prepare.html
        try
        {
            var jsonString = Encoding.UTF8.GetString(body.ToArray());
#if NET
            var jsonObject = JsonSerializer.Deserialize(jsonString, SourceGenerationContext.Default.DictionaryStringJsonElement);
#else
            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
#endif
            if (jsonObject == null)
            {
                return;
            }

            // extract model specific attributes based on model name
            if (modelName.Contains("amazon.nova"))
            {
                ProcessNovaModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
            else if (modelName.Contains("amazon.titan"))
            {
                ProcessTitanModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
            else if (modelName.Contains("anthropic.claude"))
            {
                ProcessClaudeModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
            else if (modelName.Contains("meta.llama3"))
            {
                ProcessLlamaModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
            else if (modelName.Contains("cohere.command"))
            {
                ProcessCommandModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
            else if (modelName.Contains("ai21.jamba"))
            {
                ProcessJambaModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
            else if (modelName.Contains("mistral.mistral"))
            {
                ProcessMistralModelAttributes(activity, jsonObject, isRequest, awsSemanticConventions);
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessNovaModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("inferenceConfig", out var inferenceConfig))
                {
                    if (inferenceConfig.TryGetProperty("top_p", out var topP))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                    }

                    if (inferenceConfig.TryGetProperty("temperature", out var temperature))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                    }

                    if (inferenceConfig.TryGetProperty("max_new_tokens", out var maxTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                    }
                }
            }
            else
            {
                if (jsonBody.TryGetValue("usage", out var usage))
                {
                    if (usage.TryGetProperty("inputTokens", out var inputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, inputTokens.GetInt32());
                    }

                    if (usage.TryGetProperty("outputTokens", out var outputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, outputTokens.GetInt32());
                    }
                }

                if (jsonBody.TryGetValue("stopReason", out var finishReasons))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessTitanModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("textGenerationConfig", out var textGenerationConfig))
                {
                    if (textGenerationConfig.TryGetProperty("topP", out var topP))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                    }

                    if (textGenerationConfig.TryGetProperty("temperature", out var temperature))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                    }

                    if (textGenerationConfig.TryGetProperty("maxTokenCount", out var maxTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                    }
                }
            }
            else
            {
                if (jsonBody.TryGetValue("inputTextTokenCount", out var inputTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, inputTokens.GetInt32());
                }

                if (jsonBody.TryGetValue("results", out var resultsArray))
                {
                    var results = resultsArray[0];
                    if (results.TryGetProperty("tokenCount", out var outputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, outputTokens.GetInt32());
                    }

                    if (results.TryGetProperty("completionReason", out var finishReasons))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessClaudeModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("top_p", out var topP))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                }

                if (jsonBody.TryGetValue("temperature", out var temperature))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                }

                if (jsonBody.TryGetValue("max_tokens", out var maxTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                }
            }
            else
            {
                if (jsonBody.TryGetValue("usage", out var usage))
                {
                    if (usage.TryGetProperty("input_tokens", out var inputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, inputTokens.GetInt32());
                    }

                    if (usage.TryGetProperty("output_tokens", out var outputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, outputTokens.GetInt32());
                    }
                }

                if (jsonBody.TryGetValue("stop_reason", out var finishReasons))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessLlamaModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("top_p", out var topP))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                }

                if (jsonBody.TryGetValue("temperature", out var temperature))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                }

                if (jsonBody.TryGetValue("max_gen_len", out var maxTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                }
            }
            else
            {
                if (jsonBody.TryGetValue("prompt_token_count", out var inputTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, inputTokens.GetInt32());
                }

                if (jsonBody.TryGetValue("generation_token_count", out var outputTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, outputTokens.GetInt32());
                }

                if (jsonBody.TryGetValue("stop_reason", out var finishReasons))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessCommandModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("p", out var topP))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                }

                if (jsonBody.TryGetValue("temperature", out var temperature))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                }

                if (jsonBody.TryGetValue("max_tokens", out var maxTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                }

                // input tokens not provided in Command response body, so we estimate the value based on input length
                if (jsonBody.TryGetValue("message", out var input))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, Convert.ToInt32(Math.Ceiling((double)(input.GetString()?.Length ?? 0) / 6)));
                }
            }
            else
            {
                if (jsonBody.TryGetValue("finish_reason", out var finishReasons))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                }

                // completion tokens not provided in Command response body, so we estimate the value based on output length
                if (jsonBody.TryGetValue("text", out var output))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, Convert.ToInt32(Math.Ceiling((double)(output.GetString()?.Length ?? 0) / 6)));
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessJambaModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("top_p", out var topP))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                }

                if (jsonBody.TryGetValue("temperature", out var temperature))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                }

                if (jsonBody.TryGetValue("max_tokens", out var maxTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                }
            }
            else
            {
                if (jsonBody.TryGetValue("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var inputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, inputTokens.GetInt32());
                    }

                    if (usage.TryGetProperty("completion_tokens", out var outputTokens))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, outputTokens.GetInt32());
                    }
                }

                if (jsonBody.TryGetValue("choices", out var choices))
                {
                    if (choices[0].TryGetProperty("finish_reason", out var finishReasons))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }

    private static void ProcessMistralModelAttributes(Activity activity, Dictionary<string, JsonElement> jsonBody, bool isRequest, AWSSemanticConventions awsSemanticConventions)
    {
        try
        {
            if (isRequest)
            {
                if (jsonBody.TryGetValue("top_p", out var topP))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTopP(activity, topP.GetDouble());
                }

                if (jsonBody.TryGetValue("temperature", out var temperature))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiTemperature(activity, temperature.GetDouble());
                }

                if (jsonBody.TryGetValue("max_tokens", out var maxTokens))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiMaxTokens(activity, maxTokens.GetInt32());
                }

                // input tokens not provided in Mistral response body, so we estimate the value based on input length
                if (jsonBody.TryGetValue("prompt", out var input))
                {
                    awsSemanticConventions.TagBuilder.SetTagAttributeGenAiInputTokens(activity, Convert.ToInt32(Math.Ceiling((double)(input.GetString()?.Length ?? 0) / 6)));
                }
            }
            else
            {
                if (jsonBody.TryGetValue("outputs", out var outputsArray))
                {
                    var output = outputsArray[0];
                    if (output.TryGetProperty("stop_reason", out var finishReasons))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiFinishReasons(activity, [finishReasons.GetString() ?? string.Empty]);
                    }

                    // output tokens not provided in Mistral response body, so we estimate the value based on output length
                    if (output.TryGetProperty("text", out var text))
                    {
                        awsSemanticConventions.TagBuilder.SetTagAttributeGenAiOutputTokens(activity, Convert.ToInt32(Math.Ceiling((double)(text.GetString()?.Length ?? 0) / 6)));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AWSInstrumentationEventSource.Log.JsonParserException(nameof(AWSLlmModelProcessor), ex);
        }
    }
}
