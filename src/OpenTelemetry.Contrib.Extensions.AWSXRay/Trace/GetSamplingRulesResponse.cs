using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
internal class GetSamplingRulesResponse
{
    public string NextToken { get; }

    public List<SamplingRuleRecord> SamplingRuleRecords { get; }

    internal class SamplingRuleRecord
    {
        public double? CreatedAt { get; }

        public double? ModifiedAt { get; }

        public SamplingRule SamplingRule { get; }
    }
}
