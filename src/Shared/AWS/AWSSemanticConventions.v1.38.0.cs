// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.AWS;

// disable Style Warnings to improve readability of this specific file.
#pragma warning disable SA1124
#pragma warning disable SA1005
#pragma warning disable SA1514
#pragma warning disable SA1201
#pragma warning disable SA1516

internal partial class AWSSemanticConventions
{
    /// <summary>
    /// Open Telemetry Semantic Conventions as of 1.29.0:
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.29.0.
    /// </summary>
    private class AWSSemanticConventions_V1_38_0 : AWSSemanticConventions_V1_29_0
    {
        public override string AttributeFaasInstanceId => "faas.instance.id";
        public override string AttributeFaasInstance => string.Empty;
        public override string AttributeFaasFunctionId => "faas.function.id";
        public override string AttributeFaasID => string.Empty;
    }
}
