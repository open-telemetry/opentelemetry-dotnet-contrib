param(
  [Parameter(Position = 0)]
  [string]$Command = 'help'
)

$scriptRoot = $PSScriptRoot

function Invoke-WeaverCheck
{
  docker run --rm `
    -u 1000:1000 `
    -v "${scriptRoot}:/workspace" `
    -w /workspace `
    -e HOME=/tmp `
    otel/weaver:v0.23.0 `
    registry check `
    -r ./model `
    --v2
}

function Invoke-WeaverGenerate
{
  docker run --rm `
    -u 1000:1000 `
    -v "${scriptRoot}:/workspace" `
    -w /workspace `
    -e HOME=/tmp `
    otel/weaver:v0.23.0 `
    registry generate `
    -r ./model `
    --v2 `
    markdown `
    ./docs
}

function Invoke-WeaverPackage
{
  docker run --rm `
    -u 1000:1000 `
    -v "${scriptRoot}:/workspace" `
    -w /workspace `
    -e HOME=/tmp `
    otel/weaver:v0.23.0 `
    registry package `
    -r ./model `
    --v2 `
    --resolved-schema-uri http://example.com/model/v1 `
    -o ./model-out
}

$usage = 'Usage: .\weaver.ps1 [check|generate|package]'

switch ($Command.ToLowerInvariant())
{
  'check'
  {
    Invoke-WeaverCheck
  }

  'generate'
  {
    Invoke-WeaverGenerate
  }

  'package'
  {
    Invoke-WeaverPackage
  }

  'help'
  {
    Write-Host $usage
  }

  default
  {
    throw "Unknown command '$Command'. $usage"
  }
}
