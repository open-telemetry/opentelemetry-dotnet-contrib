// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Running;
using OpenTelemetry.Instrumentation.EntityFrameworkCore.Benchmarks;

var summaries = BenchmarkSwitcher.FromAssembly(typeof(EntityFrameworkCoreBenchmarks).Assembly).Run(args);
return summaries.SelectMany(p => p.Reports).Any((p) => !p.Success) ? 1 : 0;
