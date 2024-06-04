// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
using OpenTelemetry.Resources.Container.Models;

namespace OpenTelemetry.Resources.Container;

/// <summary>
/// "Source Generation" is feature added to System.Text.Json in .NET 6.0.
/// This is a performance optimization that avoids runtime reflection when performing serialization.
/// Serialization metadata will be computed at compile-time and included in the assembly.
/// <see href="https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation-modes" />.
/// <see href="https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation" />.
/// <see href="https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/" />.
/// </summary>
[JsonSerializable(typeof(K8sPod))]
[JsonSerializable(typeof(K8sPodStatus))]
[JsonSerializable(typeof(K8sContainerStatus))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}
#endif
