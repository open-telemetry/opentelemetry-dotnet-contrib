#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $script:scriptPath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "report-unreleased-changes.ps1"

    # Creates an isolated repository fixture with a Directory.Packages.props (the
    # script reads the latest OpenTelemetry core version from it) and returns its
    # root path. The script only reads files, so the real working tree is never
    # touched.
    function script:NewRepositoryFixture {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path $work -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup><OpenTelemetryCoreLatestVersion>1.12.0</OpenTelemetryCoreLatestVersion></PropertyGroup></Project>"
        return $work
    }

    function script:NewPackage {
        param([string]$Root, [string]$Name, [string]$Changelog)

        $packageDir = Join-Path -Path $Root -ChildPath "src/$Name"
        New-Item -Path $packageDir -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $packageDir -ChildPath "CHANGELOG.md") -Value $Changelog
        return $packageDir
    }
}

Describe "report-unreleased-changes.ps1" {

    It "reports unreleased changes for a package" {
        $work = NewRepositoryFixture
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Foo" -Changelog @"
# Changelog

## Unreleased

* Added the thing.

## 1.0.0

Released 2024-01-01
"@ | Out-Null

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "# Unreleased Changes" -Because "the report should have a top-level heading"
        $output | Should-MatchString ':package: Instrumentation\.Foo' -Because "the package should be listed in the summary table by its short name"
        $output | Should-MatchString '## OpenTelemetry\.Instrumentation\.Foo' -Because "the package should have its own changes section"
        $output | Should-MatchString '\* Added the thing\.' -Because "the unreleased changes should be included"
    }

    It "suggests next versions for packages in a range of states" {
        $work = NewRepositoryFixture

        # Tracks OTel core (major/minor within range) and records a core bump in
        # its unreleased section, so the suggested version follows the core version.
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Tracking" -Changelog @"
# Changelog

## Unreleased

* Updated OpenTelemetry core component version(s) to ``1.13.0``.

## 1.12.0

Released 2024-01-01
"@ | Out-Null

        # Has never been released, so it should be suggested as an initial alpha.
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.New" -Changelog @"
# Changelog

## Unreleased

* The first change.
"@ | Out-Null

        # Last release was a prerelease, so the prerelease number should advance.
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Pre" -Changelog @"
# Changelog

## Unreleased

* A change.

## 1.12.0-beta.1

Released 2024-01-01
"@ | Out-Null

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "# Unreleased Changes"
        $output | Should-MatchString '`1\.13\.0`' -Because "a package tracking the core version should follow the recorded core bump"
        $output | Should-MatchString '`1\.12\.0-alpha\.1`' -Because "an unreleased package should be suggested as an initial alpha based on the core version"
        $output | Should-MatchString '`1\.12\.0-beta\.2`' -Because "a package last released as a prerelease should advance the prerelease number"
        $output | Should-MatchString "_All changes - this package has not yet been released._" -Because "a never-released package should be flagged as such"
    }

    It "reports that there are no unreleased changes when none are present" {
        $work = NewRepositoryFixture
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Foo" -Changelog @"
# Changelog

## Unreleased

## 1.0.0

Released 2024-01-01
"@ | Out-Null

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "No packages have any unreleased changes" -Because "an empty unreleased section should be treated as no changes"
        $output | Should-NotMatchString "# Unreleased Changes" -Because "the full report should not be produced when there is nothing to report"
    }

    It "ignores changelogs that have no unreleased section" {
        $work = NewRepositoryFixture
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Foo" -Changelog @"
# Changelog

## 1.0.0

Released 2024-01-01
"@ | Out-Null

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "No packages have any unreleased changes" -Because "a changelog without an Unreleased heading contributes nothing"
    }

    It "omits packages whose unreleased section is empty" {
        $work = NewRepositoryFixture
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Foo" -Changelog @"
# Changelog

## Unreleased

* A real change.

## 1.0.0

Released 2024-01-01
"@ | Out-Null
        NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Empty" -Changelog @"
# Changelog

## Unreleased

## 1.0.0

Released 2024-01-01
"@ | Out-Null

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString ':package: Instrumentation\.Foo' -Because "the package with changes should be listed"
        $output | Should-NotMatchString 'Instrumentation\.Empty' -Because "the package without changes should be omitted"
    }

    It "does not modify the working tree" {
        $work = NewRepositoryFixture
        $packageDir = NewPackage -Root $work -Name "OpenTelemetry.Instrumentation.Foo" -Changelog @"
# Changelog

## Unreleased

* A change.

## 1.0.0

Released 2024-01-01
"@
        $changelogPath = Join-Path -Path $packageDir -ChildPath "CHANGELOG.md"
        $before = Get-Content -Path $changelogPath -Raw

        & $scriptPath -repoRoot $work | Out-Null

        Get-Content -Path $changelogPath -Raw | Should-Be $before -Because "the report script must only read, never modify, the changelog files"
    }
}
