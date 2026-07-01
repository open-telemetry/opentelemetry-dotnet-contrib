#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "post-release.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "post-release" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "CreateRelease" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"
    }

    It "creates a non-prerelease draft release with notes from the CHANGELOG and then publishes it" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.9.0

* Added a new feature.

## 1.8.0

Released 2024-01-01
"@

        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateRelease -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" -tag "foo-1.9.0" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "release" -and
            $args -contains "create" -and
            $args -contains "foo-1.9.0" -and
            $args -contains "--latest" -and
            $args -contains "--draft" -and
            (($args -join " ") -match "OpenTelemetry.Instrumentation.Foo v1\.9\.0") -and
            (($args -join " ") -match "Added a new feature\.")
        } -Because "a stable release should be created as a draft with notes built from the CHANGELOG"

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "release" -and $args -contains "edit" -and $args -contains "--draft=false"
        } -Because "the release should be published once created"
    }

    It "creates a prerelease draft release for a prerelease tag" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.9.0-alpha.1

* A prerelease change.

## 1.8.0

Released 2024-01-01
"@

        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateRelease -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" -tag "foo-1.9.0-alpha.1" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "create" -and
            $args -contains "foo-1.9.0-alpha.1" -and
            $args -contains "--prerelease" -and
            $args -contains "--draft"
        } -Because "a prerelease tag should create a prerelease draft"
    }

    It "throws when the tag cannot be parsed" {
        { CreateRelease -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" -tag "noprefix" 6>$null } |
            Should -Throw "*Could not parse prefix or version from tag*" -Because "a tag without a prefix cannot be parsed"
    }
}

Describe "TryPostPackagesReadyNoticeOnPrepareReleasePullRequest" {

    It "posts a packages-ready notice on the matching pull request" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") {
                return '[{"number":42,"author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.9.0","comments":[{"author":{"login":"otelbot"},"body":"I just pushed the [foo-1.9.0](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/releases/tag/foo-1.9.0) tag."}]}]'
            }
            return $null
        }

        TryPostPackagesReadyNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -tag "foo-1.9.0" `
            -tagSha "abc123" `
            -packagesUrl "https://example.com/packages" `
            -expectedPrAuthorUserName "otelbot" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "available on NuGet")
        } -Because "a packages-ready notice should be posted on the matching prepare release PR"
    }

    It "does nothing when no pull request is found for the tag commit" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") { return "[]" }
            return $null
        }

        TryPostPackagesReadyNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -tag "foo-1.9.0" `
            -tagSha "abc123" `
            -packagesUrl "https://example.com/packages" `
            -expectedPrAuthorUserName "otelbot" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -ParameterFilter {
            $args -contains "comment"
        } -Because "no notice should be posted when no pull request matches the commit"
    }

    It "does nothing when the matching pull request has no pushed-tag comment" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") {
                return '[{"number":42,"author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.9.0","comments":[]}]'
            }
            return $null
        }

        TryPostPackagesReadyNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -tag "foo-1.9.0" `
            -tagSha "abc123" `
            -packagesUrl "https://example.com/packages" `
            -expectedPrAuthorUserName "otelbot" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -ParameterFilter {
            $args -contains "comment"
        } -Because "a notice is only posted when the pushed-tag comment is present"
    }
}

Describe "CreatePackageValidationBaselineVersionUpdatePullRequest" {

    It "updates PackageValidationBaselineVersion and opens a labelled pull request" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix><PackageValidationBaselineVersion>0.0.0</PackageValidationBaselineVersion></PropertyGroup></Project>"
        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup></PropertyGroup></Project>"

        Mock -CommandName "git" -ModuleName "post-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { $null }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePackageValidationBaselineVersionUpdatePullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -tag "foo-1.2.3" 6>$null
        }
        finally {
            Pop-Location
        }

        (Get-Content -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") -Raw) |
            Should -Match "<PackageValidationBaselineVersion>1\.2\.3</PackageValidationBaselineVersion>" -Because "the baseline version should be bumped to the released version"

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and
            $args -contains "create" -and
            $args -contains "--label" -and
            $args -contains "release" -and
            (($args -join " ") -match "foo- stable release 1\.2\.3")
        } -Because "a labelled pull request should be opened for the baseline update"
    }
}

Describe "CreateOpenTelemetryCoreLatestVersionUpdatePullRequest" {

    It "returns without opening a pull request for a non-core tag" {
        Mock -CommandName "git" -ModuleName "post-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { $null }

        CreateOpenTelemetryCoreLatestVersionUpdatePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -tag "foo-1.2.3" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -Because "only core and coreunstable tags trigger a core version update"
    }

    It "updates the core version, opens a PR and updates affected CHANGELOGs" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup><OpenTelemetryCoreLatestVersion>1.0.0</OpenTelemetryCoreLatestVersion></PropertyGroup></Project>"

        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

* An existing change.

## 1.0.0

Released 2024-01-01
"@

        Mock -CommandName "git" -ModuleName "post-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if (($args -contains "pr") -and ($args -contains "create")) {
                return "https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/789"
            }
            return $null
        }
        # Mock the internal dependency lookup to avoid running 'dotnet restore'. It
        # reads the current OpenTelemetryCoreLatestVersion so the "before" and
        # "after" snapshots differ once the script updates Directory.Packages.props.
        Mock -CommandName "GetCoreDependenciesForProjects" -ModuleName "post-release" -MockWith {
            $version = (Select-String -Path "Directory.Packages.props" -Pattern "<OpenTelemetryCoreLatestVersion>(.*?)</OpenTelemetryCoreLatestVersion>").Matches[0].Groups[1].Value
            return @{ (Join-Path -Path $PWD -ChildPath "src/OpenTelemetry.Instrumentation.Foo") = @{ "OpenTelemetry" = $version } }
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateOpenTelemetryCoreLatestVersionUpdatePullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -tag "core-1.2.3" 6>$null
        }
        finally {
            Pop-Location
        }

        (Get-Content -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") -Raw) |
            Should -Match "<OpenTelemetryCoreLatestVersion>1\.2\.3</OpenTelemetryCoreLatestVersion>" -Because "the core version should be bumped in Directory.Packages.props"

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and $args -contains "create" -and $args -contains "--label" -and $args -contains "release"
        } -Because "a labelled pull request should be opened for the core update"

        (Get-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Raw) |
            Should -Match "Updated OpenTelemetry core component version\(s\) to ``1\.2\.3``" -Because "the CHANGELOG of an affected project should be updated"
    }

    It "updates the core unstable version for a coreunstable tag" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup><OpenTelemetryCoreUnstableLatestVersion>1.0.0-beta.1</OpenTelemetryCoreUnstableLatestVersion></PropertyGroup></Project>"

        Mock -CommandName "git" -ModuleName "post-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if (($args -contains "pr") -and ($args -contains "create")) {
                return "https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/790"
            }
            return $null
        }
        # No projects change, so the CHANGELOG update path is skipped.
        Mock -CommandName "GetCoreDependenciesForProjects" -ModuleName "post-release" -MockWith { @{} }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateOpenTelemetryCoreLatestVersionUpdatePullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -tag "coreunstable-1.1.0-beta.1" 6>$null
        }
        finally {
            Pop-Location
        }

        (Get-Content -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") -Raw) |
            Should -Match "<OpenTelemetryCoreUnstableLatestVersion>1\.1\.0-beta\.1</OpenTelemetryCoreUnstableLatestVersion>" -Because "the core unstable version should be bumped"

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and $args -contains "create" -and $args -contains "--label" -and $args -contains "release"
        } -Because "a labelled pull request should be opened for the core unstable update"
    }
}

Describe "GetCoreDependenciesForProjects" {

    It "returns only the dependency map and does not leak command output" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") -Value "<Project></Project>"

        $assetsDirectory = Join-Path -Path $work -ChildPath "artifacts/obj/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $assetsDirectory -ItemType Directory -Force | Out-Null

        $assets = @'
{
  "OpenTelemetry": {
    "target": "Package",
    "version": "1.9.0"
  },
  "Newtonsoft.Json": {
    "target": "Package",
    "version": "13.0.0"
  }
}
'@
        Set-Content -Path (Join-Path -Path $assetsDirectory -ChildPath "project.assets.json") -Value $assets

        $result = InModuleScope -ModuleName "post-release" -Parameters @{ Work = $work } {
            param($Work)

            # Simulate 'dotnet restore' writing progress output to the success
            # stream. If that output is not suppressed it pollutes the return value.
            Mock -CommandName "dotnet" -MockWith {
                Write-Output "Determining projects to restore..."
                Write-Output "Restored OpenTelemetry.Instrumentation.Foo.csproj"
            }

            Push-Location -Path $Work -ErrorAction Stop
            try {
                GetCoreDependenciesForProjects
            }
            finally {
                Pop-Location
            }
        }

        @($result).Count | Should -Be 1 -Because "'dotnet restore' output must be piped to Out-Null so it does not leak into the return value"
        $result | Should -BeOfType [hashtable] -Because "the function should return only the dependency map"

        $dependencies = $result.Values | Select-Object -First 1
        $dependencies["OpenTelemetry"] | Should -Be "1.9.0" -Because "the version should be parsed from project.assets.json"
        $dependencies.ContainsKey("Newtonsoft.Json") | Should -BeFalse -Because "only OpenTelemetry core packages should be tracked"
    }
}

Describe "UpdateCommonPropsVersion" {

    It "updates the common props version when the tag prefix and version match" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path $work -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup><OpenTelemetryInstrumentationFooLatestStableVersion>0.0.0</OpenTelemetryInstrumentationFooLatestStableVersion></PropertyGroup></Project>"

        InModuleScope -ModuleName "post-release" -Parameters @{ Work = $work } {
            param($Work)

            Mock -CommandName "git" -MockWith { $global:LASTEXITCODE = 0 }

            Push-Location -Path $Work -ErrorAction Stop
            try {
                UpdateCommonPropsVersion `
                    -tagPrefix "Instrumentation.Foo-" `
                    -version "1.0.0" `
                    -propertyName "Instrumentation.Foo-" `
                    -propertyDisplayName "OpenTelemetryInstrumentationFooLatestStableVersion" 6>$null
            }
            finally {
                Pop-Location
            }

            Should -Invoke -CommandName "git" -ParameterFilter {
                $args -contains "add"
            } -Because "the updated props file should be staged"
        }

        (Get-Content -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") -Raw) |
            Should -Match "<OpenTelemetryInstrumentationFooLatestStableVersion>1\.0\.0</OpenTelemetryInstrumentationFooLatestStableVersion>" -Because "the version should be updated in Directory.Packages.props"
    }
}
