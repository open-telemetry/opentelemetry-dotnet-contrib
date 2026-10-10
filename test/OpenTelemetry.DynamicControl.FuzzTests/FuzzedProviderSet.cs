// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public readonly record struct FuzzedProviderSet(ImmutableArray<FuzzedProvider> Providers);
