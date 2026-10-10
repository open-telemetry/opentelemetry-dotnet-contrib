// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.FuzzTests;

public readonly record struct FuzzedProvider(
    byte KindSelector,
    int? Priority,
    int PolicyMask,
    double SamplingProbability,
    byte LogLevelSelector);
