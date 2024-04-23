# Semantic Conventions for OpenTelemetry .NET

This project contains the generated code for the [Semantic Conventions](https://github.com/open-telemetry/semantic-conventions)
defined by the OpenTelemetry specification.

## Installation

```shell
dotnet add package OpenTelemetry.SemanticConventions --prerelease
```

## Generating the files

This project uses the
[Semantic Convention Generator](https://github.com/open-telemetry/build-tools/blob/main/semantic-conventions/README.md).
The folder `scripts` at the top level of the project contains the template
and the script files used in the process.

To generate the code files, run:

```shell
./scripts/generate.sh
```

Or, with PowerShell:

```shell
./scripts/generate.ps1
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Build tools](https://github.com/open-telemetry/build-tools)
