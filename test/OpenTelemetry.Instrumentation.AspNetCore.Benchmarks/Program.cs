// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Running;
using OpenTelemetry.Instrumentation.AspNetCore.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(AspNetCoreBenchmarks).Assembly).Run(args);
