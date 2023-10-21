// <copyright file="SourceGenerationContext.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Text.Json.Serialization;

namespace OpenTelemetry.ResourceDetectors.Azure;

#if NET6_0_OR_GREATER
/// <summary>
/// "Source Generation" is feature added to System.Text.Json in .NET 6.0.
/// This is a performance optimization that avoids runtime reflection when performing serialization.
/// Serialization metadata will be computed at compile-time and included in the assembly.
/// <see href="https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation-modes" />.
/// <see href="https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation" />.
/// <see href="https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/" />.
/// </summary>
[JsonSerializable(typeof(AzureVmMetadataResponse))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}
#endif
