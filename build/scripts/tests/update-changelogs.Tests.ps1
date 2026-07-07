#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $script:scriptPath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "update-changelogs.ps1"
}

Describe "update-changelogs.ps1" {

    It "updates the CHANGELOG only for projects matching the tag prefix" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $matchingProject = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        $otherProject = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Other"

        New-Item -Path $matchingProject -ItemType Directory -Force | Out-Null
        New-Item -Path $otherProject -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $matchingProject -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content `
            -Path (Join-Path -Path $otherProject -ChildPath "OpenTelemetry.Instrumentation.Other.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>other-</MinVerTagPrefix></PropertyGroup></Project>"

        Set-Content `
            -Path (Join-Path -Path $matchingProject -ChildPath "CHANGELOG.md") `
            -Value "# Changelog`n`n## Unreleased`n`n* Some change`n"
        Set-Content `
            -Path (Join-Path -Path $otherProject -ChildPath "CHANGELOG.md") `
            -Value "# Changelog`n`n## Unreleased`n`n* Other change`n"

        # The script formats the release date with 'Get-Date -UFormat', so the
        # expected value is computed using the same call.
        $expectedReleaseDate = Get-Date -UFormat '%Y-%b-%d'

        Push-Location -Path $work -ErrorAction Stop
        try {
            # 6> redirects the Information stream so the test output stays quiet.
            & $scriptPath -minVerTagPrefix "foo-" -version "1.2.3" 6>$null
        }
        finally {
            Pop-Location
        }

        $matchingChangelog = Get-Content -Path (Join-Path -Path $matchingProject -ChildPath "CHANGELOG.md") -Raw
        $matchingChangelog | Should-MatchString "## 1\.2\.3" -Because "the version heading should be added for a matching project"
        $matchingChangelog | Should-BeLikeString "*Released $expectedReleaseDate*" -Because "a release date should be added for a matching project"
        $matchingChangelog | Should-MatchString "\* Some change" -Because "existing changelog entries should be preserved"

        $otherChangelog = Get-Content -Path (Join-Path -Path $otherProject -ChildPath "CHANGELOG.md") -Raw
        $otherChangelog | Should-NotMatchString "## 1\.2\.3" -Because "projects with a different tag prefix should not be updated"
    }
}
