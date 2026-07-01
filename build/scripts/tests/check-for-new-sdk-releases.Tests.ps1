#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $script:scriptPath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "check-for-new-sdk-releases.ps1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    # Creates an isolated repository fixture containing a Directory.Packages.props
    # with the three OpenTelemetry core version properties set to the supplied
    # values and returns its root path. The script only reads the file.
    function script:NewRepositoryFixture {
        param(
            [string]$StableVersion,
            [string]$PrereleaseVersion,
            [string]$UnstableVersion
        )

        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path $work -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value @"
<Project>
  <PropertyGroup>
    <OpenTelemetryCoreLatestVersion>$StableVersion</OpenTelemetryCoreLatestVersion>
    <OpenTelemetryCoreUnstableLatestVersion>$UnstableVersion</OpenTelemetryCoreUnstableLatestVersion>
    <OpenTelemetryCoreLatestPrereleaseVersion>$PrereleaseVersion</OpenTelemetryCoreLatestPrereleaseVersion>
  </PropertyGroup>
</Project>
"@
        return $work
    }

    # Builds the JSON payload returned by 'gh release list
    # --json isPrerelease,publishedAt,tagName' for a set of tags, assigning each an
    # increasing publishedAt timestamp so the later entries are treated as the most
    # recent releases. By default a release is flagged as a prerelease when its tag
    # has a prerelease suffix, matching how the opentelemetry-dotnet repository marks
    # its releases; the isPrerelease flag can be overridden for a specific tag via the
    # -Prereleases hashtable to verify that the script classifies releases by version
    # suffix alone and deliberately ignores the GitHub prerelease flag.
    function script:NewReleasesJson {
        param(
            [string[]]$Tags,
            [hashtable]$Prereleases = @{}
        )

        $index = 0
        $releases = foreach ($tag in $Tags) {
            $index++
            $version = $tag -replace '^(coreunstable-|core-)', ''
            $isPrerelease = if ($Prereleases.ContainsKey($tag)) { [bool]$Prereleases[$tag] } else { $version.Contains("-") }
            [PSCustomObject]@{
                tagName      = $tag
                isPrerelease = $isPrerelease
                publishedAt  = "2024-01-{0:D2}T00:00:00Z" -f $index
            }
        }

        return ConvertTo-Json -InputObject @($releases) -Depth 5
    }
}

AfterAll {
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "check-for-new-sdk-releases.ps1" {

    It "does not trigger the update workflow when all versions are up to date" {
        $work = NewRepositoryFixture -StableVersion "1.16.0" -PrereleaseVersion "1.16.0-rc.1" -UnstableVersion "1.16.0-beta.1"

        $releasesJson = NewReleasesJson -Tags @(
            "core-1.15.0",
            "coreunstable-1.15.0-beta.1",
            "core-1.16.0-rc.1",
            "coreunstable-1.16.0-beta.1",
            "core-1.16.0"
        )

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 1; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 6>$null

        Should -Invoke -CommandName "gh" -Exactly -Times 0 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run"
        } -Because "no version is out of date so the update workflow should not be triggered"
    }

    It "triggers the update workflow with the correct tag for each out-of-date version" {
        $work = NewRepositoryFixture -StableVersion "1.15.0" -PrereleaseVersion "1.15.0-rc.1" -UnstableVersion "1.15.0-beta.1"

        $releasesJson = NewReleasesJson -Tags @(
            "core-1.15.0",
            "coreunstable-1.15.0-beta.1",
            "core-1.16.0-rc.1",
            "coreunstable-1.16.0-beta.1",
            "core-1.16.0"
        )

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 1; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 6>$null

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run" -and $args -contains "tag=core-1.16.0"
        } -Because "the stable version should be updated using the 'core-' prefixed tag"

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run" -and $args -contains "tag=core-1.16.0-rc.1"
        } -Because "the prerelease version should be updated using the 'core-' prefixed release candidate tag"

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run" -and $args -contains "tag=coreunstable-1.16.0-beta.1"
        } -Because "the unstable version should be updated using the 'coreunstable-' prefixed tag"
    }

    It "only triggers the update workflow for the versions that are out of date" {
        $work = NewRepositoryFixture -StableVersion "1.15.0" -PrereleaseVersion "1.16.0-rc.1" -UnstableVersion "1.16.0-beta.1"

        $releasesJson = NewReleasesJson -Tags @(
            "core-1.16.0-rc.1",
            "coreunstable-1.16.0-beta.1",
            "core-1.16.0"
        )

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 1; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 6>$null

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run"
        } -Because "only the stable version is out of date"

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run" -and $args -contains "tag=core-1.16.0"
        } -Because "the out-of-date stable version should be updated and not mistaken for a 'coreunstable-' release"
    }

    It "classifies releases by version suffix and not the GitHub prerelease flag so it agrees with the core version update workflow" {
        $work = NewRepositoryFixture -StableVersion "1.16.0" -PrereleaseVersion "2.1.0-rc.1" -UnstableVersion "1.16.0-beta.1"

        # 'core-2.0.0' has no prerelease version suffix but is flagged as a prerelease
        # by GitHub. The core-version-update workflow decides whether a tag updates the
        # stable or prerelease property based solely on the version suffix, so this
        # script must ignore the flag and treat 'core-2.0.0' as the latest *stable*
        # release. If the flag were honoured instead, 'core-2.0.0' would be classified
        # as a prerelease (which is already up to date at 'core-2.1.0-rc.1') and the
        # stable property would be left behind at '1.16.0'.
        $releasesJson = NewReleasesJson `
            -Tags @("core-1.16.0", "coreunstable-1.16.0-beta.1", "core-2.0.0", "core-2.1.0-rc.1") `
            -Prereleases @{ "core-2.0.0" = $true }

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 1; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 6>$null

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run"
        } -Because "only the stable version is out of date once the prerelease flag is ignored"

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run" -and $args -contains "tag=core-2.0.0"
        } -Because "'core-2.0.0' has no version suffix so it must update the stable property despite the prerelease flag"
    }

    It "does not trigger the update workflow when the update branch already exists" {
        $work = NewRepositoryFixture -StableVersion "1.15.0" -PrereleaseVersion "1.16.0-rc.1" -UnstableVersion "1.16.0-beta.1"

        # Only the stable version is out of date; the prerelease and unstable releases
        # match the props so the branch guard is only exercised for the stable update.
        $releasesJson = NewReleasesJson -Tags @("core-1.16.0", "core-1.16.0-rc.1", "coreunstable-1.16.0-beta.1")

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 0; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 6>$null

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "api" -and ($args -join " ") -match "branches/otelbot/post-core-1\.16\.0-update"
        } -Because "the script should check whether the update branch already exists before dispatching"

        Should -Invoke -CommandName "gh" -Exactly -Times 0 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run"
        } -Because "an update for this version is already in progress so it should not be dispatched again"
    }

    It "uses the GITHUB_REPOSITORY environment variable to determine the repository to dispatch to" {
        $work = NewRepositoryFixture -StableVersion "1.15.0" -PrereleaseVersion "1.16.0-rc.1" -UnstableVersion "1.16.0-beta.1"

        $releasesJson = NewReleasesJson -Tags @("core-1.16.0")

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 1; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        $originalRepository = $env:GITHUB_REPOSITORY
        $env:GITHUB_REPOSITORY = "open-telemetry/my-fork"
        try {
            & $scriptPath -repoRoot $work 6>$null
        }
        finally {
            $env:GITHUB_REPOSITORY = $originalRepository
        }

        Should -Invoke -CommandName "gh" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run" -and $args -contains "open-telemetry/my-fork"
        } -Because "the workflow should be dispatched to the repository named by GITHUB_REPOSITORY"
    }

    It "warns and does not trigger the update workflow when no matching release can be found" {
        $work = NewRepositoryFixture -StableVersion "1.15.0" -PrereleaseVersion "1.15.0-rc.1" -UnstableVersion "1.15.0-beta.1"

        # Only stable releases exist, so no release candidate or unstable release can be found.
        $releasesJson = NewReleasesJson -Tags @("core-1.15.0")

        Mock -CommandName "gh" -MockWith {
            if ($args -contains "api") { $global:LASTEXITCODE = 1; return }
            $global:LASTEXITCODE = 0
            if ($args -contains "list") { return $releasesJson }
        }

        $warnings = & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 3>&1 6>$null

        Should -Invoke -CommandName "gh" -Exactly -Times 0 -ParameterFilter {
            $args -contains "workflow" -and $args -contains "run"
        } -Because "no version is out of date and the missing release kinds should be skipped"

        ($warnings -join "`n") | Should -Match "Could not find a prerelease release" -Because "a warning should be emitted when a matching release is missing"
        ($warnings -join "`n") | Should -Match "Could not find an? unstable release" -Because "a warning should be emitted when a matching release is missing"
    }

    It "throws when an expected version property is missing from Directory.Packages.props" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path $work -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup><OpenTelemetryCoreLatestVersion>1.16.0</OpenTelemetryCoreLatestVersion></PropertyGroup></Project>"

        Mock -CommandName "gh" -MockWith { $global:LASTEXITCODE = 0 }

        { & $scriptPath -repoRoot $work -contribRepository "open-telemetry/opentelemetry-dotnet-contrib" 6>$null } |
            Should -Throw "*OpenTelemetryCoreLatestPrereleaseVersion*"
    }
}
