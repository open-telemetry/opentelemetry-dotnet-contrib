// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Benchmarks;

public sealed class BenchmarkItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;
}
