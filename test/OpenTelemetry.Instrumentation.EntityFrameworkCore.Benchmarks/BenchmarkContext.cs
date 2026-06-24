// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Benchmarks;

internal sealed class BenchmarkContext(DbContextOptions<BenchmarkContext> options)
    : DbContext(options)
{
    public DbSet<BenchmarkItem> Items { get; set; } = null!;
}
