// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

/// <summary>
/// Tests that configure the instrumentation share static state (the instrumentation options and the transport
/// settings loaders), so they are placed in the same collection to prevent them from running in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ServiceFabricRemotingCollectionDefinition
{
    public const string Name = "ServiceFabricRemoting";
}
