# Assembly Metadata Resource Detector

| Status      |                                                    |
|-------------|----------------------------------------------------|
| Stability   | [Alpha](../../README.md#alpha)                     |
| Code Owners | [@austindrenski](https://github.com/austindrenski) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.AssemblyMetadata)](https://www.nuget.org/packages/OpenTelemetry.Resources.AssemblyMetadata)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.AssemblyMetadata)](https://www.nuget.org/packages/OpenTelemetry.Resources.AssemblyMetadata)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.AssemblyMetadata)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.AssemblyMetadata)

## Getting Started

You need to install the `OpenTelemetry.Resources.AssemblyMetadata` package to be able to use the Assembly Metadata Resource Detector.

```shell
dotnet add package --prerelease OpenTelemetry.Resources.AssemblyMetadata
```

## Usage

You can add the Assembly Metadata Resource Detector to the `ResourceBuilder` as shown by the following example:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(static builder => builder.AddAssemblyMetadataDetector())
    .WithLogging(static builder => /* ... */)
    .WithMetrics(static builder => /* ... */)
    .WithTracing(static builder => /* ... */);
```

By default, the resource detector is added with the [`Assembly`][assembly] returned by [`Assembly.GetEntryAssembly()`][entry_assembly] and detects any
[AssemblyMetadataAttribute][assembly_metadata_attribute] applied to it with a [key][assembly_metadata_attribute_key] prefixed by `otel:`.

Alternatively, you can add the resource detector with a specific [`Assembly`][assembly]:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(static builder => builder.AddAssemblyMetadataDetector(typeof(SomeType).Assembly))
    .WithLogging(static builder => /* ... */)
    .WithMetrics(static builder => /* ... */)
    .WithTracing(static builder => /* ... */);
```

The following example demonstrates how you can re-use MSBuild properties populated by [SourceLink][source_link] to populate [`vcs`][otel_vcs] resource
attributes in an MSBuild project file:

```msbuild
<Project>

  <ItemGroup>
    <AssemblyMetadata Include="otel:vcs.ref.head.name" Value="$(SourceBranchName)" />
    <AssemblyMetadata Include="otel:vcs.ref.head.revision" Value="$(SourceRevisionId)" />
    <AssemblyMetadata Include="otel:vcs.repository.url.full" Value="$(PrivateRepositoryUrl)" />
  </ItemGroup>

</Project>
```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

[//]: # (links)

[assembly]: https://learn.microsoft.com/dotnet/api/system.reflection.assembly

[assembly_metadata_attribute]: https://learn.microsoft.com/dotnet/api/system.reflection.assemblymetadataattribute

[assembly_metadata_attribute_key]: https://learn.microsoft.com/dotnet/api/system.reflection.assemblymetadataattribute.key

[entry_assembly]: https://learn.microsoft.com/dotnet/api/system.reflection.assembly.getentryassembly

[source_link]: https://github.com/dotnet/sourcelink

[otel_vcs]: https://opentelemetry.io/docs/specs/semconv/registry/attributes/vcs/
