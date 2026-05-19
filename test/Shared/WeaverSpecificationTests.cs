// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Tests;

[Collection(WeaverCollection.Name)]
[Trait("Category", "Weaver")]
public abstract class WeaverSpecificationTests(WeaverFixture fixture, ITestOutputHelper outputHelper) : IDisposable
{
    ~WeaverSpecificationTests()
    {
        this.Dispose(disposing: false);
    }

    protected WeaverFixture Fixture { get; } = fixture;

    protected ITestOutputHelper OutputHelper { get; } = outputHelper;

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected async Task AssertTelemetryConformsToSemanticConventions(
        (IReadOnlyList<Activity> Traces, IReadOnlyList<Metric> Metrics) telemetry,
        Version semanticConventionsVersion,
        IReadOnlyList<KeyValuePair<string, string?>>? suppressAdvice = null,
        CancellationToken cancellationToken = default) =>
        await WeaverTelemetryVerifier.VerifyAsync(
            telemetry,
            semanticConventionsVersion,
            this.Fixture,
            this.OutputHelper,
            suppressAdvice,
            cancellationToken);

    protected virtual void Dispose(bool disposing)
    {
        // No-op
    }
}
