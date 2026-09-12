// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

// Tests that drive the OpenTelemetry logging pipeline recycle records through
// the process-wide LogRecordSharedPool singleton. Running two such classes
// concurrently corrupts that shared state, so they share this collection and
// run sequentially.
[CollectionDefinition("LogPipeline", DisableParallelization = true)]
public sealed class LogPipelineDefinition
{
}
