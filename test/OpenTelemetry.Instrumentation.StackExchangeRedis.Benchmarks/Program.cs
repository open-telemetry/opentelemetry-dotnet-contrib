// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Running;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

BenchmarkSwitcher.FromAssembly(typeof(RedisFixture).Assembly).Run(args);
