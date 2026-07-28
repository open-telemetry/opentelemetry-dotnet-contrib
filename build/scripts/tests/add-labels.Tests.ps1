#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "add-labels.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "add-labels" -Force -ErrorAction SilentlyContinue
    Remove-Module -Name "build" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "AddLabelsOnIssuesForComponentFoundInBody" {

    It "adds a component label when a component is referenced in the issue body" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith { }

        AddLabelsOnIssuesForComponentFoundInBody `
            -issueNumber 123 `
            -issueLabels "" `
            -issueBody "## Component`n`nOpenTelemetry.Instrumentation.Foo"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "issue" -and
            $args -contains "edit" -and
            $args -contains "--add-label" -and
            $args -contains "comp:instrumentation.foo"
        } -Because "the component named in the body should be added as a lower-cased 'comp:' label"
    }

    It "does nothing when no component is referenced in the issue body" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith { }

        AddLabelsOnIssuesForComponentFoundInBody `
            -issueNumber 123 `
            -issueLabels "" `
            -issueBody "This issue does not mention a component."

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 0 -Because "no label should be changed when no component is referenced"
    }
}

Describe "AddLabelsOnPullRequestsBasedOnFilesChanged" {

    # Note: the mock bodies execute in the module's scope (because of
    # -ModuleName) so they must return literal values rather than referencing
    # variables defined in the test scope.

    It "adds component and infrastructure labels based on the files changed" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith {
            if (($args -contains "label") -and ($args -contains "list")) {
                return '[{"name":"comp:instrumentation.foo","id":"1"},{"name":"infra","id":"2"},{"name":"documentation","id":"3"}]'
            }
            if ($args -contains "diff") {
                return @("src/OpenTelemetry.Instrumentation.Foo/Internal/Foo.cs", "build/scripts/post-release.psm1")
            }
            if ($args -contains "view") {
                return '{"labels":[]}'
            }
            return $null
        }

        AddLabelsOnPullRequestsBasedOnFilesChanged -pullRequestNumber 456 -labelPackagePrefix "comp:"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "comp:instrumentation.foo"
        } -Because "a change under src/OpenTelemetry.Instrumentation.Foo should add the matching component label"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "infra"
        } -Because "a change under build/ should add the infra label"
    }

    It "removes a managed label that no longer applies" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith {
            if (($args -contains "label") -and ($args -contains "list")) {
                return '[{"name":"comp:instrumentation.foo","id":"1"},{"name":"infra","id":"2"},{"name":"documentation","id":"3"}]'
            }
            if ($args -contains "diff") {
                return @("README.md")
            }
            if ($args -contains "view") {
                return '{"labels":[{"name":"infra"}]}'
            }
            return $null
        }

        AddLabelsOnPullRequestsBasedOnFilesChanged -pullRequestNumber 456 -labelPackagePrefix "comp:"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "documentation"
        } -Because "README.md is a documentation file so the documentation label should be added"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--remove-label" -and $args -contains "infra"
        } -Because "the existing infra label no longer applies and should be removed"
    }

    It "adds perf and dependencies labels and does not re-add an existing label" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith {
            if (($args -contains "label") -and ($args -contains "list")) {
                return '[{"name":"comp:instrumentation.foo","id":"1"},{"name":"infra","id":"2"}]'
            }
            if ($args -contains "diff") {
                return @(
                    "src/OpenTelemetry.Instrumentation.Foo/Foo.cs",
                    "test/OpenTelemetry.Instrumentation.Foo.Benchmarks/Bench.cs",
                    "test/benchmarks/Suite.cs",
                    "Directory.Packages.props"
                )
            }
            if ($args -contains "view") {
                return '{"labels":[{"name":"comp:instrumentation.foo"}]}'
            }
            return $null
        }

        AddLabelsOnPullRequestsBasedOnFilesChanged -pullRequestNumber 456 -labelPackagePrefix "comp:"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "perf"
        } -Because "benchmark and stress projects should add the perf label"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "dependencies"
        } -Because "changes to Directory.Packages.props should add the dependencies label"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Times 0 -ParameterFilter {
            $args -contains "--add-label" -and $args -contains "comp:instrumentation.foo"
        } -Because "a label already present on the pull request should not be added again"
    }
}
