#!/usr/bin/env pwsh

#Requires -PSEdition Core
#Requires -Version 7

<#
.SYNOPSIS
Checks whether the OpenTelemetry core package versions used by this repository are
up to date with the latest releases in the opentelemetry-dotnet repository and, if
not, triggers the "Core version update" workflow to open a pull request updating them.

.DESCRIPTION
The OpenTelemetry core package versions are pinned in Directory.Packages.props using
three properties, each of which corresponds to a release tag in the
opentelemetry-dotnet repository:

  * OpenTelemetryCoreLatestVersion           -> latest stable release            (e.g. core-1.16.0)
  * OpenTelemetryCoreLatestPrereleaseVersion -> latest release candidate release (e.g. core-1.16.0-rc.1)
  * OpenTelemetryCoreUnstableLatestVersion   -> latest unstable (beta) release   (e.g. coreunstable-1.16.0-beta.1)

For each property the script finds the latest matching release in the
opentelemetry-dotnet repository and, if it does not match the version pinned in
Directory.Packages.props, runs the core-version-update workflow with the
corresponding tag as the input as described in the opentelemetry-dotnet releasing
documentation.

.EXAMPLE
./build/scripts/check-for-new-sdk-releases.ps1
#>

param(
  [Parameter()][string]$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName,
  [Parameter()][string]$coreRepository = "open-telemetry/opentelemetry-dotnet",
  [Parameter()][string]$contribRepository,
  [Parameter()][string]$workflow = "core-version-update.yml"
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Maps each version property in Directory.Packages.props to the release tag prefix
# used in the opentelemetry-dotnet repository and the kind of release it tracks.
# Stable and release-candidate releases both use the 'core-' tag prefix and are
# distinguished by whether the version has a prerelease suffix; unstable (beta)
# releases use the 'coreunstable-' tag prefix.
$propertyMappings = @(
  [PSCustomObject]@{ Property = "OpenTelemetryCoreLatestVersion";           TagPrefix = "core-";         Kind = "stable" }
  [PSCustomObject]@{ Property = "OpenTelemetryCoreLatestPrereleaseVersion"; TagPrefix = "core-";         Kind = "prerelease" }
  [PSCustomObject]@{ Property = "OpenTelemetryCoreUnstableLatestVersion";   TagPrefix = "coreunstable-"; Kind = "unstable" }
)

function GetPropertyVersions {
  param([string]$propsPath)

  $content = Get-Content -Path $propsPath -Raw

  $versions = @{}
  foreach ($mapping in $propertyMappings) {
    $match = [regex]::Match($content, "<$($mapping.Property)>(?<version>[^<]+)</$($mapping.Property)>")
    if ($match.Success -eq $false) {
      throw "Could not find property '$($mapping.Property)' in '$propsPath'."
    }
    $versions[$mapping.Property] = $match.Groups["version"].Value.Trim()
  }

  return $versions
}

function GetLatestCoreVersion {
  param(
    [object[]]$releases,
    [string]$tagPrefix,
    [string]$kind
  )

  # Releases are ordered most-recent first, so the first match for each kind is the
  # latest release of that kind.
  foreach ($release in $releases) {
    $tag = $release.tagName

    if ($tag.StartsWith($tagPrefix) -eq $false) {
      continue
    }

    # 'core-' is also a prefix of 'coreunstable-' so make sure unstable releases are
    # not mistaken for stable or release candidate releases.
    if ($tagPrefix -eq "core-" -and $tag.StartsWith("coreunstable-") -eq $true) {
      continue
    }

    $version = $tag.Substring($tagPrefix.Length)

    # A release is treated as a prerelease if either its version has a prerelease
    # suffix (e.g. core-1.16.0-rc.1) or the GitHub release itself is flagged as a
    # prerelease. Stable releases (e.g. core-1.16.0) have neither, whereas release
    # candidate and unstable (beta) releases have both.
    $isPrerelease = $version.Contains("-") -or ($release.isPrerelease -eq $true)

    $isMatch =
      ($kind -eq "stable" -and $isPrerelease -eq $false) -or
      ($kind -eq "prerelease" -and $isPrerelease -eq $true) -or
      ($kind -eq "unstable")

    if ($isMatch -eq $true) {
      return $version
    }
  }

  return $null
}

if ([string]::IsNullOrEmpty($contribRepository) -eq $true) {
  $contribRepository = if ([string]::IsNullOrEmpty($env:GITHUB_REPOSITORY) -eq $true) {
    "open-telemetry/opentelemetry-dotnet-contrib"
  }
  else {
    $env:GITHUB_REPOSITORY
  }
}

$propsPath = Join-Path -Path $repoRoot -ChildPath "Directory.Packages.props"
$versions = GetPropertyVersions -propsPath $propsPath

$releasesJson = gh release list `
  --repo $coreRepository `
  --exclude-drafts `
  --limit 100 `
  --json isPrerelease,publishedAt,tagName

if ($LASTEXITCODE -gt 0) {
  throw "Failed to list releases for '$coreRepository'."
}

$releases = @($releasesJson | ConvertFrom-Json | Sort-Object -Property publishedAt -Descending)

$triggered = $false

foreach ($mapping in $propertyMappings) {
  $currentVersion = $versions[$mapping.Property]
  $latestVersion = GetLatestCoreVersion -releases $releases -tagPrefix $mapping.TagPrefix -kind $mapping.Kind

  if ([string]::IsNullOrEmpty($latestVersion) -eq $true) {
    Write-Warning "Could not find a $($mapping.Kind) release in '$coreRepository' for property '$($mapping.Property)'."
    continue
  }

  if ($latestVersion -eq $currentVersion) {
    Write-Information "$($mapping.Property) is up to date ($currentVersion)."
    continue
  }

  $tag = "$($mapping.TagPrefix)$latestVersion"

  Write-Information "$($mapping.Property) is out of date (current: $currentVersion, latest: $latestVersion). Triggering '$workflow' for tag '$tag'."

  gh workflow run $workflow `
    --repo $contribRepository `
    --field tag=$tag

  if ($LASTEXITCODE -gt 0) {
    throw "Failed to trigger '$workflow' for tag '$tag'."
  }

  $triggered = $true
}

if ($triggered -eq $false) {
  Write-Information "All OpenTelemetry core package versions are up to date."
}
