// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Running;
using OpenTelemetry.Instrumentation.AspNet.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(AspNetInstrumentationBenchmarks).Assembly).Run(args);
