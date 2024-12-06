// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.AWS;

// disable Style Warnings to improve readability of this specific file.
#pragma warning disable SA1124
#pragma warning disable SA1005
#pragma warning disable SA1514
#pragma warning disable SA1201
#pragma warning disable SA1516

internal static partial class AWSSemanticConventions
{
    /// <summary>
    /// Open Telemetry Semantic Conventions as of 1.29.0:
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.29.0.
    /// </summary>
    private class AWSSemanticConventions_v1_29_0 : AWSSemanticConventions_v1_27_0
    {
        // AWS Attributes
        public override string AttributeAWSBedrock => "aws.bedrock";
        public override string AttributeAWSDynamoTableName => "aws.dynamodb.table_names";

        // FAAS Attributes
        public override string AttributeFaasID => "cloud.resource_id";
        public override string AttributeFaasExecution => "faas.invocation_id";

        // HTTP Attributes
        public override string AttributeHttpStatusCode => this.AttributeHttpResponseStatusCode;
        public override string AttributeHttpScheme => this.AttributeUrlScheme;

        //public override string AttributeHttpTarget => string.Empty; // value no longer written ---- move to new PR
        public override string AttributeHttpMethod => this.AttributeHttpRequestMethod;
        public override string AttributeHttpResponseStatusCode => "http.response.status_code";
        public override string AttributeHttpRequestMethod => "http.request.method";

        // NET Attributes
        public override string AttributeNetHostName => this.AttributeServerAddress;
        public override string AttributeNetHostPort => this.AttributeServerPort;

        // SERVER Attributes
        public override string AttributeServerAddress => "server.address";
        public override string AttributeServerPort => "server.port";

        // URL Attributes
        public override string AttributeUrlPath => "url.path";
        public override string AttributeUrlQuery => "url.query";
        public override string AttributeUrlScheme => "url.scheme";
    }
}
