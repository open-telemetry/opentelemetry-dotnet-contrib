// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Avoid mutations to RuntimePipelineCustomizerRegistry.Instance causing flaky tests
using Xunit.v3;

[assembly: Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
