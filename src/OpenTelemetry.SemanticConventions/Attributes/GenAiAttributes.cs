// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class GenAiAttributes
{
    /// <summary>
    /// Deprecated, use Event API to report completions contents.
    /// </summary>
    [Obsolete("Removed, no replacement at this time.")]
    public const string AttributeGenAiCompletion = "gen_ai.completion";

    /// <summary>
    /// The response format that is requested.
    /// </summary>
    public const string AttributeGenAiOpenaiRequestResponseFormat = "gen_ai.openai.request.response_format";

    /// <summary>
    /// Deprecated, use <c>gen_ai.request.seed</c>.
    /// </summary>
    [Obsolete("Replaced by <c>gen_ai.request.seed</c> attribute.")]
    public const string AttributeGenAiOpenaiRequestSeed = "gen_ai.openai.request.seed";

    /// <summary>
    /// The service tier requested. May be a specific tier, default, or auto.
    /// </summary>
    public const string AttributeGenAiOpenaiRequestServiceTier = "gen_ai.openai.request.service_tier";

    /// <summary>
    /// The service tier used for the response.
    /// </summary>
    public const string AttributeGenAiOpenaiResponseServiceTier = "gen_ai.openai.response.service_tier";

    /// <summary>
    /// A fingerprint to track any eventual change in the Generative AI environment.
    /// </summary>
    public const string AttributeGenAiOpenaiResponseSystemFingerprint = "gen_ai.openai.response.system_fingerprint";

    /// <summary>
    /// The name of the operation being performed.
    /// </summary>
    /// <remarks>
    /// If one of the predefined values applies, but specific system uses a different name it's RECOMMENDED to document it in the semantic conventions for specific GenAI system and use system-specific name in the instrumentation. If a different name is not documented, instrumentation libraries SHOULD use applicable predefined value.
    /// </remarks>
    public const string AttributeGenAiOperationName = "gen_ai.operation.name";

    /// <summary>
    /// Deprecated, use Event API to report prompt contents.
    /// </summary>
    [Obsolete("Removed, no replacement at this time.")]
    public const string AttributeGenAiPrompt = "gen_ai.prompt";

    /// <summary>
    /// The encoding formats requested in an embeddings operation, if specified.
    /// </summary>
    /// <remarks>
    /// In some GenAI systems the encoding formats are called embedding types. Also, some GenAI systems only accept a single format per request.
    /// </remarks>
    public const string AttributeGenAiRequestEncodingFormats = "gen_ai.request.encoding_formats";

    /// <summary>
    /// The frequency penalty setting for the GenAI request.
    /// </summary>
    public const string AttributeGenAiRequestFrequencyPenalty = "gen_ai.request.frequency_penalty";

    /// <summary>
    /// The maximum number of tokens the model generates for a request.
    /// </summary>
    public const string AttributeGenAiRequestMaxTokens = "gen_ai.request.max_tokens";

    /// <summary>
    /// The name of the GenAI model a request is being made to.
    /// </summary>
    public const string AttributeGenAiRequestModel = "gen_ai.request.model";

    /// <summary>
    /// The presence penalty setting for the GenAI request.
    /// </summary>
    public const string AttributeGenAiRequestPresencePenalty = "gen_ai.request.presence_penalty";

    /// <summary>
    /// Requests with same seed value more likely to return same result.
    /// </summary>
    public const string AttributeGenAiRequestSeed = "gen_ai.request.seed";

    /// <summary>
    /// List of sequences that the model will use to stop generating further tokens.
    /// </summary>
    public const string AttributeGenAiRequestStopSequences = "gen_ai.request.stop_sequences";

    /// <summary>
    /// The temperature setting for the GenAI request.
    /// </summary>
    public const string AttributeGenAiRequestTemperature = "gen_ai.request.temperature";

    /// <summary>
    /// The top_k sampling setting for the GenAI request.
    /// </summary>
    public const string AttributeGenAiRequestTopK = "gen_ai.request.top_k";

    /// <summary>
    /// The top_p sampling setting for the GenAI request.
    /// </summary>
    public const string AttributeGenAiRequestTopP = "gen_ai.request.top_p";

    /// <summary>
    /// Array of reasons the model stopped generating tokens, corresponding to each generation received.
    /// </summary>
    public const string AttributeGenAiResponseFinishReasons = "gen_ai.response.finish_reasons";

    /// <summary>
    /// The unique identifier for the completion.
    /// </summary>
    public const string AttributeGenAiResponseId = "gen_ai.response.id";

    /// <summary>
    /// The name of the model that generated the response.
    /// </summary>
    public const string AttributeGenAiResponseModel = "gen_ai.response.model";

    /// <summary>
    /// The Generative AI product as identified by the client or server instrumentation.
    /// </summary>
    /// <remarks>
    /// The <c>gen_ai.system</c> describes a family of GenAI models with specific model identified
    /// by <c>gen_ai.request.model</c> and <c>gen_ai.response.model</c> attributes.
    /// <p>
    /// The actual GenAI product may differ from the one identified by the client.
    /// Multiple systems, including Azure OpenAI and Gemini, are accessible by OpenAI client
    /// libraries. In such cases, the <c>gen_ai.system</c> is set to <c>openai</c> based on the
    /// instrumentation's best knowledge, instead of the actual system. The <c>server.address</c>
    /// attribute may help identify the actual system in use for <c>openai</c>.
    /// <p>
    /// For custom model, a custom friendly name SHOULD be used.
    /// If none of these options apply, the <c>gen_ai.system</c> SHOULD be set to <c>_OTHER</c>.
    /// </remarks>
    public const string AttributeGenAiSystem = "gen_ai.system";

    /// <summary>
    /// The type of token being counted.
    /// </summary>
    public const string AttributeGenAiTokenType = "gen_ai.token.type";

    /// <summary>
    /// Deprecated, use <c>gen_ai.usage.output_tokens</c> instead.
    /// </summary>
    [Obsolete("Replaced by <c>gen_ai.usage.output_tokens</c> attribute.")]
    public const string AttributeGenAiUsageCompletionTokens = "gen_ai.usage.completion_tokens";

    /// <summary>
    /// The number of tokens used in the GenAI input (prompt).
    /// </summary>
    public const string AttributeGenAiUsageInputTokens = "gen_ai.usage.input_tokens";

    /// <summary>
    /// The number of tokens used in the GenAI response (completion).
    /// </summary>
    public const string AttributeGenAiUsageOutputTokens = "gen_ai.usage.output_tokens";

    /// <summary>
    /// Deprecated, use <c>gen_ai.usage.input_tokens</c> instead.
    /// </summary>
    [Obsolete("Replaced by <c>gen_ai.usage.input_tokens</c> attribute.")]
    public const string AttributeGenAiUsagePromptTokens = "gen_ai.usage.prompt_tokens";

    /// <summary>
    /// The response format that is requested.
    /// </summary>
    public static class GenAiOpenaiRequestResponseFormatValues
    {
        /// <summary>
        /// Text response format.
        /// </summary>
        public const string Text = "text";

        /// <summary>
        /// JSON object response format.
        /// </summary>
        public const string JsonObject = "json_object";

        /// <summary>
        /// JSON schema response format.
        /// </summary>
        public const string JsonSchema = "json_schema";
    }

    /// <summary>
    /// The service tier requested. May be a specific tier, default, or auto.
    /// </summary>
    public static class GenAiOpenaiRequestServiceTierValues
    {
        /// <summary>
        /// The system will utilize scale tier credits until they are exhausted.
        /// </summary>
        public const string Auto = "auto";

        /// <summary>
        /// The system will utilize the default scale tier.
        /// </summary>
        public const string Default = "default";
    }

    /// <summary>
    /// The name of the operation being performed.
    /// </summary>
    public static class GenAiOperationNameValues
    {
        /// <summary>
        /// Chat completion operation such as <a href="https://platform.openai.com/docs/api-reference/chat">OpenAI Chat API</a>.
        /// </summary>
        public const string Chat = "chat";

        /// <summary>
        /// Text completions operation such as <a href="https://platform.openai.com/docs/api-reference/completions">OpenAI Completions API (Legacy)</a>.
        /// </summary>
        public const string TextCompletion = "text_completion";

        /// <summary>
        /// Embeddings operation such as <a href="https://platform.openai.com/docs/api-reference/embeddings/create">OpenAI Create embeddings API</a>.
        /// </summary>
        public const string Embeddings = "embeddings";
    }

    /// <summary>
    /// The Generative AI product as identified by the client or server instrumentation.
    /// </summary>
    public static class GenAiSystemValues
    {
        /// <summary>
        /// OpenAI.
        /// </summary>
        public const string Openai = "openai";

        /// <summary>
        /// Vertex AI.
        /// </summary>
        public const string VertexAi = "vertex_ai";

        /// <summary>
        /// Gemini.
        /// </summary>
        public const string Gemini = "gemini";

        /// <summary>
        /// Anthropic.
        /// </summary>
        public const string Anthropic = "anthropic";

        /// <summary>
        /// Cohere.
        /// </summary>
        public const string Cohere = "cohere";

        /// <summary>
        /// Azure AI Inference.
        /// </summary>
        public const string AzAiInference = "az.ai.inference";

        /// <summary>
        /// Azure OpenAI.
        /// </summary>
        public const string AzAiOpenai = "az.ai.openai";

        /// <summary>
        /// IBM Watsonx AI.
        /// </summary>
        public const string IbmWatsonxAi = "ibm.watsonx.ai";

        /// <summary>
        /// AWS Bedrock.
        /// </summary>
        public const string AwsBedrock = "aws.bedrock";

        /// <summary>
        /// Perplexity.
        /// </summary>
        public const string Perplexity = "perplexity";

        /// <summary>
        /// xAI.
        /// </summary>
        public const string Xai = "xai";

        /// <summary>
        /// DeepSeek.
        /// </summary>
        public const string Deepseek = "deepseek";

        /// <summary>
        /// Groq.
        /// </summary>
        public const string Groq = "groq";

        /// <summary>
        /// Mistral AI.
        /// </summary>
        public const string MistralAi = "mistral_ai";
    }

    /// <summary>
    /// The type of token being counted.
    /// </summary>
    public static class GenAiTokenTypeValues
    {
        /// <summary>
        /// Input tokens (prompt, input, etc.).
        /// </summary>
        public const string Input = "input";

        /// <summary>
        /// Output tokens (completion, response, etc.).
        /// </summary>
        public const string Completion = "output";
    }
}
