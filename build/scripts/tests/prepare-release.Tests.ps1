#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "prepare-release.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "prepare-release" -Force -ErrorAction SilentlyContinue
    Remove-Module -Name "build" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "CreatePullRequestToUpdateChangelogsAndPublicApis" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"

        # Create stubs for the scripts that are invoked via a relative path so
        # the real CHANGELOG/public API files are never modified. The
        # finalize-publicapi stub records that it ran so the test can assert it
        # is only invoked for stable releases.
        $stubScripts = Join-Path -Path $work -ChildPath "build/scripts"
        New-Item -Path $stubScripts -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $stubScripts -ChildPath "update-changelogs.ps1") `
            -Value "param([string]`$minVerTagPrefix, [string]`$version)"
        Set-Content `
            -Path (Join-Path -Path $stubScripts -ChildPath "finalize-publicapi.ps1") `
            -Value "param([string]`$minVerTagPrefix)`nSet-Content -Path 'finalize-called.txt' -Value 'called'"

        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith { $null }
    }

    It "throws when the version <Version> is not valid" -ForEach @(
        @{ Version = "not-a-version" }
        @{ Version = "1.2" }
        @{ Version = "1.2.3.4" }
        @{ Version = "1.2.3-preview.1" }
    ) {
        {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -component "OpenTelemetry.Instrumentation.Foo" `
                -version $Version `
                -requestedByUserName "someone"
        } | Should -Throw "*did not match expected format*" -Because "'$Version' is not a valid release version"
    }

    It "throws when the project file has no MinVerTagPrefix" {
        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo/OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup></PropertyGroup></Project>"

        Push-Location -Path $work -ErrorAction Stop
        try {
            {
                CreatePullRequestToUpdateChangelogsAndPublicApis `
                    -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                    -component "OpenTelemetry.Instrumentation.Foo" `
                    -version "1.2.3" `
                    -requestedByUserName "someone" 6>$null
            } | Should -Throw "*Could not parse MinVerTagPrefix*"
        }
        finally {
            Pop-Location
        }
    }

    It "opens a pull request and finalizes public APIs for a stable release" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -component "OpenTelemetry.Instrumentation.Foo" `
                -version "1.2.3" `
                -requestedByUserName "someone" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "switch" -and $args -contains "--create" -and ($args -join " ") -match "otelbot/prepare-foo-1\.2\.3-release"
        } -Because "a release branch should be created for the version"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and
            $args -contains "create" -and
            $args -contains "--label" -and
            $args -contains "release" -and
            (($args -join " ") -match "\[release\] Prepare release foo-1\.2\.3")
        } -Because "a labelled pull request should be opened for the release"

        (Join-Path -Path $work -ChildPath "finalize-called.txt") | Should -Exist -Because "public APIs should be finalized for a stable release"
    }

    It "configures the git user when a name and email are provided" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -component "OpenTelemetry.Instrumentation.Foo" `
                -version "1.2.3" `
                -requestedByUserName "someone" `
                -gitUserName "otelbot" `
                -gitUserEmail "otelbot@example.com" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "config" -and $args -contains "user.name" -and $args -contains "otelbot"
        } -Because "the git user name should be configured when provided"
        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "config" -and $args -contains "user.email" -and $args -contains "otelbot@example.com"
        } -Because "the git user email should be configured when provided"
    }

    It "prepares a prerelease without finalizing public APIs" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -component "OpenTelemetry.Instrumentation.Foo" `
                -version "1.2.3-alpha.1" `
                -requestedByUserName "someone" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "create" -and (($args -join " ") -match "Prepare release foo-1\.2\.3-alpha\.1")
        } -Because "a prerelease pull request should still be opened"

        (Join-Path -Path $work -ChildPath "finalize-called.txt") | Should -Not -Exist -Because "public APIs should not be finalized for a prerelease"
    }

    It "throws when creating the release branch fails" {
        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 1 }

        Push-Location -Path $work -ErrorAction Stop
        try {
            {
                CreatePullRequestToUpdateChangelogsAndPublicApis `
                    -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                    -component "OpenTelemetry.Instrumentation.Foo" `
                    -version "1.2.3" `
                    -requestedByUserName "someone" 6>$null
            } | Should -Throw "*git switch failure*" -Because "a failure to create the branch should stop the release"
        }
        finally {
            Pop-Location
        }
    }

    It "throws when committing the changes fails" {
        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith {
            $global:LASTEXITCODE = if ($args -contains "commit") { 1 } else { 0 }
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            {
                CreatePullRequestToUpdateChangelogsAndPublicApis `
                    -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                    -component "OpenTelemetry.Instrumentation.Foo" `
                    -version "1.2.3" `
                    -requestedByUserName "someone" 6>$null
            } | Should -Throw "*git commit failure*"
        }
        finally {
            Pop-Location
        }
    }
}

Describe "LockPullRequestAndPostNoticeToCreateReleaseTag" {

    It "posts a notice and locks the pull request" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.2.3","mergeCommit":{"oid":"abc123"}}'
            }
            return $null
        }

        LockPullRequestAndPostNoticeToCreateReleaseTag `
            -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "/CreateReleaseTag")
        } -Because "a comment offering to create the release tag should be posted"
        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "lock"
        } -Because "the pull request should be locked after posting the notice"
    }

    It "throws when the pull request author is not the expected user" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            return '{"author":{"login":"someone-else"},"title":"[release] Prepare release foo-1.2.3","mergeCommit":{"oid":"abc123"}}'
        }

        {
            LockPullRequestAndPostNoticeToCreateReleaseTag `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot"
        } | Should -Throw "*PR author was unexpected*" -Because "only pull requests opened by the expected bot should be processed"
    }
}

Describe "CreateReleaseTagAndPostNoticeOnPullRequest" {

    It "creates and pushes the release tag and posts a notice" {
        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
        # Avoid the real 10 second sleep the function uses to dodge a race condition.
        Mock -CommandName "Start-Sleep" -ModuleName "prepare-release" -MockWith { }
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.2.3","mergeCommit":{"oid":"abc123"}}'
            }
            return $null
        }

        CreateReleaseTagAndPostNoticeOnPullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot" 6>$null

        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "tag" -and $args -contains "foo-1.2.3" -and $args -contains "abc123"
        } -Because "the release tag should be created on the merge commit"
        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "foo-1\.2\.3")
        } -Because "a notice about the pushed tag should be posted on the pull request"
    }
}

Describe "UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"

        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
    }

    It "updates the release date and comments on the pull request" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.2.3

Released 0000-00-00

* An item.
"@

        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-foo-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"write"}'
            }
            return $null
        }

        # The script formats the release date with 'Get-Date -UFormat', so the
        # expected value is computed using the same call.
        $expectedReleaseDate = Get-Date -UFormat '%Y-%b-%d'

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "maintainer" 6>$null
        }
        finally {
            Pop-Location
        }

        $changelog = Get-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Raw
        $changelog | Should -BeLike "*Released $expectedReleaseDate*" -Because "the placeholder release date should be replaced with today's date"
        $changelog | Should -Not -Match "0000-00-00" -Because "the placeholder date should no longer be present"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "I updated the CHANGELOG release dates")
        } -Because "a notice should confirm the dates were updated"
    }

    It "comments that no update was needed when the dates are already valid" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 9.9.9

Released 2020-01-01
"@

        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-foo-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"write"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "maintainer" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "valid release dates")
        } -Because "the function reports when no CHANGELOG needed updating"
    }

    It "refuses to update when the commenter lacks write permission" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-foo-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release foo-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"read"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "drive-by" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "don't have permission")
        } -Because "only maintainers and approvers may update the PR"
    }
}

Describe "TagCodeOwnersOnOrRunWorkflowForRequestReleaseIssue" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"

        # FindComponentOwners (imported from build.psm1) reads the component
        # owners file relative to the working directory.
        $githubDirectory = Join-Path -Path $work -ChildPath ".github"
        New-Item -Path $githubDirectory -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $githubDirectory -ChildPath "component_owners.yml") -Value @"
components:
  src/OpenTelemetry.Instrumentation.Foo/:
    - owner1
  src/OpenTelemetry.Instrumentation.Bar/:
    - owner2
"@

        # Stubs for the scripts CreatePullRequestToUpdateChangelogsAndPublicApis
        # invokes via a relative path so the real files are never modified.
        $stubScripts = Join-Path -Path $work -ChildPath "build/scripts"
        New-Item -Path $stubScripts -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $stubScripts -ChildPath "update-changelogs.ps1") `
            -Value "param([string]`$minVerTagPrefix, [string]`$version)"
        Set-Content `
            -Path (Join-Path -Path $stubScripts -ChildPath "finalize-publicapi.ps1") `
            -Value "param([string]`$minVerTagPrefix)"

        $script:issueBody = @"
## Component

OpenTelemetry.Instrumentation.Foo

## Version

1.2.3
"@
        $script:issueTitle = "[release request] OpenTelemetry.Instrumentation.Foo 1.2.3"

        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
    }

    It "runs the prepare release workflow when the requester has write access" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "api") {
                return '{"permission":"admin"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            TagCodeOwnersOnOrRunWorkflowForRequestReleaseIssue `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -triggeringEventName "issue_comment" `
                -approvingGroups "@open-telemetry/dotnet-contrib-maintainers" `
                -requestedByUserName "maintainer" `
                -issueNumber "55" `
                -issueBody $issueBody `
                -issueTitle $issueTitle 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and $args -contains "create" -and $args -contains "release"
        } -Because "a collaborator with write access should kick off the prepare release pull request"
        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "issue" -and $args -contains "close"
        } -Because "the release request issue should be closed once the workflow runs"
    }

    It "tags the component owners when the requester lacks permission" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "api") {
                return '{"permission":"read"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            TagCodeOwnersOnOrRunWorkflowForRequestReleaseIssue `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -triggeringEventName "issues" `
                -approvingGroups "@open-telemetry/dotnet-contrib-maintainers" `
                -requestedByUserName "contributor" `
                -issueNumber "55" `
                -issueBody $issueBody `
                -issueTitle $issueTitle 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "issue" -and $args -contains "comment" -and (($args -join " ") -match "/PrepareRelease")
        } -Because "owners and approvers should be tagged and offered the /PrepareRelease command"
        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Times 0 -ParameterFilter {
            $args -contains "pr" -and $args -contains "create"
        } -Because "no pull request should be opened without permission"
    }

    It "comments when the component in the title does not match the body" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith { $null }

        Push-Location -Path $work -ErrorAction Stop
        try {
            TagCodeOwnersOnOrRunWorkflowForRequestReleaseIssue `
                -gitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -triggeringEventName "issues" `
                -approvingGroups "@open-telemetry/dotnet-contrib-maintainers" `
                -requestedByUserName "contributor" `
                -issueNumber "55" `
                -issueBody $issueBody `
                -issueTitle "[release request] OpenTelemetry.Instrumentation.Bar 1.2.3" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "issue" -and $args -contains "comment" -and (($args -join " ") -match "does not match")
        } -Because "a mismatch between the title and body components should be reported"
    }
}
