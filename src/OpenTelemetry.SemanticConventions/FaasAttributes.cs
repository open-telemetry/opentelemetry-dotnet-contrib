// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from scripts/templates/SemanticConventionsAttributes.cs.j2</auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions
{
    /// <summary>
    /// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
    /// </summary>
    public static class FaasAttributes
    {
        /// <summary>
        /// A boolean that is true if the serverless function is executed for the first time (aka cold-start).
        /// </summary>
        public const string AttributeFaasColdstart = "faas.coldstart";

        /// <summary>
        /// A string containing the schedule period as <a href="https://docs.oracle.com/cd/E12058_01/doc/doc.1014/e12030/cron_expressions.htm">Cron Expression</a>.
        /// </summary>
        public const string AttributeFaasCron = "faas.cron";

        /// <summary>
        /// The name of the source on which the triggering operation was performed. For example, in Cloud Storage or S3 corresponds to the bucket name, and in Cosmos DB to the database name.
        /// </summary>
        public const string AttributeFaasDocumentCollection = "faas.document.collection";

        /// <summary>
        /// The document name/table subjected to the operation. For example, in Cloud Storage or S3 is the name of the file, and in Cosmos DB the table name.
        /// </summary>
        public const string AttributeFaasDocumentName = "faas.document.name";

        /// <summary>
        /// Describes the type of the operation that was performed on the data.
        /// </summary>
        public const string AttributeFaasDocumentOperation = "faas.document.operation";

        /// <summary>
        /// A string containing the time when the data was accessed in the <a href="https://www.iso.org/iso-8601-date-and-time-format.html">ISO 8601</a> format expressed in <a href="https://www.w3.org/TR/NOTE-datetime">UTC</a>.
        /// </summary>
        public const string AttributeFaasDocumentTime = "faas.document.time";

        /// <summary>
        /// The execution environment ID as a string, that will be potentially reused for other invocations to the same function/function version.
        /// </summary>
        /// <remarks>
        /// <ul>
        /// <li><strong>AWS Lambda:</strong> Use the (full) log stream name</li>
        /// </ul>.
        /// </remarks>
        public const string AttributeFaasInstance = "faas.instance";

        /// <summary>
        /// The invocation ID of the current function invocation.
        /// </summary>
        public const string AttributeFaasInvocationId = "faas.invocation_id";

        /// <summary>
        /// The name of the invoked function.
        /// </summary>
        /// <remarks>
        /// SHOULD be equal to the <c>faas.name</c> resource attribute of the invoked function.
        /// </remarks>
        public const string AttributeFaasInvokedName = "faas.invoked_name";

        /// <summary>
        /// The cloud provider of the invoked function.
        /// </summary>
        /// <remarks>
        /// SHOULD be equal to the <c>cloud.provider</c> resource attribute of the invoked function.
        /// </remarks>
        public const string AttributeFaasInvokedProvider = "faas.invoked_provider";

        /// <summary>
        /// The cloud region of the invoked function.
        /// </summary>
        /// <remarks>
        /// SHOULD be equal to the <c>cloud.region</c> resource attribute of the invoked function.
        /// </remarks>
        public const string AttributeFaasInvokedRegion = "faas.invoked_region";

        /// <summary>
        /// The amount of memory available to the serverless function converted to Bytes.
        /// </summary>
        /// <remarks>
        /// It&amp;#39;s recommended to set this attribute since e.g. too little memory can easily stop a Java AWS Lambda function from working correctly. On AWS Lambda, the environment variable <c>AWS_LAMBDA_FUNCTION_MEMORY_SIZE</c> provides this information (which must be multiplied by 1,048,576).
        /// </remarks>
        public const string AttributeFaasMaxMemory = "faas.max_memory";

        /// <summary>
        /// The name of the single function that this runtime instance executes.
        /// </summary>
        /// <remarks>
        /// This is the name of the function as configured/deployed on the FaaS
        /// platform and is usually different from the name of the callback
        /// function (which may be stored in the
        /// <a href="/docs/general/attributes.md#source-code-attributes"><c>code.namespace</c>/<c>code.function</c></a>
        /// span attributes).For some cloud providers, the above definition is ambiguous. The following
        /// definition of function name MUST be used for this attribute
        /// (and consequently the span name) for the listed cloud providers/products:<ul>
        /// <li><strong>Azure:</strong>  The full name <c>&lt;FUNCAPP&gt;/&lt;FUNC&gt;</c>, i.e., function app name
        /// followed by a forward slash followed by the function name (this form
        /// can also be seen in the resource JSON for the function).
        /// This means that a span attribute MUST be used, as an Azure function
        /// app can host multiple functions that would usually share
        /// a TracerProvider (see also the <c>cloud.resource_id</c> attribute)</li>
        /// </ul>.
        /// </remarks>
        public const string AttributeFaasName = "faas.name";

        /// <summary>
        /// A string containing the function invocation time in the <a href="https://www.iso.org/iso-8601-date-and-time-format.html">ISO 8601</a> format expressed in <a href="https://www.w3.org/TR/NOTE-datetime">UTC</a>.
        /// </summary>
        public const string AttributeFaasTime = "faas.time";

        /// <summary>
        /// Type of the trigger which caused this function invocation.
        /// </summary>
        public const string AttributeFaasTrigger = "faas.trigger";

        /// <summary>
        /// The immutable version of the function being executed.
        /// </summary>
        /// <remarks>
        /// Depending on the cloud provider and platform, use:<ul>
        /// <li><strong>AWS Lambda:</strong> The <a href="https://docs.aws.amazon.com/lambda/latest/dg/configuration-versions.html">function version</a>
        /// (an integer represented as a decimal string).</li>
        /// <li><strong>Google Cloud Run (Services):</strong> The <a href="https://cloud.google.com/run/docs/managing/revisions">revision</a>
        /// (i.e., the function name plus the revision suffix).</li>
        /// <li><strong>Google Cloud Functions:</strong> The value of the
        /// <a href="https://cloud.google.com/functions/docs/env-var#runtime_environment_variables_set_automatically"><c>K_REVISION</c> environment variable</a>.</li>
        /// <li><strong>Azure Functions:</strong> Not applicable. Do not set this attribute</li>
        /// </ul>.
        /// </remarks>
        public const string AttributeFaasVersion = "faas.version";

        /// <summary>
        /// Describes the type of the operation that was performed on the data.
        /// </summary>
        public static class FaasDocumentOperationValues
        {
            /// <summary>
            /// When a new object is created.
            /// </summary>
            public const string Insert = "insert";

            /// <summary>
            /// When an object is modified.
            /// </summary>
            public const string Edit = "edit";

            /// <summary>
            /// When an object is deleted.
            /// </summary>
            public const string Delete = "delete";
        }

        /// <summary>
        /// The cloud provider of the invoked function.
        /// </summary>
        public static class FaasInvokedProviderValues
        {
            /// <summary>
            /// Alibaba Cloud.
            /// </summary>
            public const string AlibabaCloud = "alibaba_cloud";

            /// <summary>
            /// Amazon Web Services.
            /// </summary>
            public const string Aws = "aws";

            /// <summary>
            /// Microsoft Azure.
            /// </summary>
            public const string Azure = "azure";

            /// <summary>
            /// Google Cloud Platform.
            /// </summary>
            public const string Gcp = "gcp";

            /// <summary>
            /// Tencent Cloud.
            /// </summary>
            public const string TencentCloud = "tencent_cloud";
        }

        /// <summary>
        /// Type of the trigger which caused this function invocation.
        /// </summary>
        public static class FaasTriggerValues
        {
            /// <summary>
            /// A response to some data source operation such as a database or filesystem read/write.
            /// </summary>
            public const string Datasource = "datasource";

            /// <summary>
            /// To provide an answer to an inbound HTTP request.
            /// </summary>
            public const string Http = "http";

            /// <summary>
            /// A function is set to be executed when messages are sent to a messaging system.
            /// </summary>
            public const string Pubsub = "pubsub";

            /// <summary>
            /// A function is scheduled to be executed regularly.
            /// </summary>
            public const string Timer = "timer";

            /// <summary>
            /// If none of the others apply.
            /// </summary>
            public const string Other = "other";
        }
    }
}
