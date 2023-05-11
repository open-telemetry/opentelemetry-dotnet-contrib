// <copyright file="ApplicationInsightsSamplerTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.AzureMonitor.Tests;

public class ApplicationInsightsSamplerTests
{
    [Fact]
    public void VerifyHashAlgorithmCorrectness()
    {
        byte[] testBytes1 = new byte[] // hex string: 8fffffffffffffff0000000000000000
        {
            0x8F, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF,
            0, 0, 0, 0,
            0, 0, 0, 0,
        };
        byte[] testBytes2 = new byte[] // hex string: 0f1f2f3f4f5f6f7f8f9fafbfcfdfefff
        {
            0x0F, 0x1F, 0x2F, 0x3F,
            0x4F, 0x5F, 0x6F, 0x7F,
            0x8F, 0x9F, 0xAF, 0xBF,
            0xCF, 0xDF, 0xEF, 0xFF,
        };
        ActivityTraceId testId1 = ActivityTraceId.CreateFromBytes(testBytes1);
        ActivityTraceId testId2 = ActivityTraceId.CreateFromBytes(testBytes2);

        ActivityContext parentContext = default;
        SamplingParameters testParams1 = new SamplingParameters(parentContext, testId1, "TestActivity", ActivityKind.Internal);
        SamplingParameters testParams2 = new SamplingParameters(parentContext, testId2, "TestActivity", ActivityKind.Internal);

        // Verify sample ratio: 0
        ApplicationInsightsSampler zeroSampler = new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 0 });
        Assert.Equal(SamplingDecision.RecordOnly, zeroSampler.ShouldSample(testParams1).Decision);
        Assert.Equal(SamplingDecision.RecordOnly, zeroSampler.ShouldSample(testParams2).Decision);

        // Verify sample ratio: 1
        ApplicationInsightsSampler oneSampler = new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 1 });
        Assert.Equal(SamplingDecision.RecordAndSample, oneSampler.ShouldSample(testParams1).Decision);
        Assert.Equal(SamplingDecision.RecordAndSample, oneSampler.ShouldSample(testParams2).Decision);

        // Verify sample ratio: 0.5.
        // This is below the sample score for testId2, but strict enough to drop testId1
        ApplicationInsightsSampler ratioSampler = new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 0.5f });
        Assert.Equal(SamplingDecision.RecordOnly, ratioSampler.ShouldSample(testParams1).Decision);
        Assert.Equal(SamplingDecision.RecordAndSample, ratioSampler.ShouldSample(testParams2).Decision);
    }

    [Fact]
    public void ApplicationInsightsSamplerGoodArgs()
    {
        ApplicationInsightsSampler pointFiveSampler = new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 0.5f });
        Assert.NotNull(pointFiveSampler);

        ApplicationInsightsSampler zeroSampler = new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 0f });
        Assert.NotNull(zeroSampler);

        ApplicationInsightsSampler oneSampler = new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 1f });
        Assert.NotNull(oneSampler);
    }

    [Fact]
    public void ApplicationInsightsSamplerBadArgs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = -2f }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 2f }));
    }

    [Fact]
    public void GetDescriptionMatchesSpec()
    {
        var expectedDescription = "ApplicationInsightsSampler{0.5}";
        Assert.Equal(expectedDescription, new ApplicationInsightsSampler(new ApplicationInsightsSamplerOptions { SamplingRatio = 0.5f }).Description);
    }
}
