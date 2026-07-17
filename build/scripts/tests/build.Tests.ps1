#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "build.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "build" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "ResolveProject" {

    It "resolves a named project to its build project file" {
        $title = $null
        $project = $null
        $component = $null

        ResolveProject `
            -projectNameOrComponentData "OpenTelemetry.Contrib" `
            -title ([ref]$title) `
            -project ([ref]$project) `
            -component ([ref]$component)

        $title | Should-Be "OpenTelemetry.Contrib.proj"
        $project | Should-Be "./build/Projects/OpenTelemetry.Contrib.proj"
        $component | Should-BeNull -Because "a named project is not a component build"
    }

    It "resolves a Component[...] value to the shared component build project" {
        $title = $null
        $project = $null
        $component = $null

        ResolveProject `
            -projectNameOrComponentData "Component[OpenTelemetry.Instrumentation.Foo]" `
            -title ([ref]$title) `
            -project ([ref]$project) `
            -component ([ref]$component)

        $component | Should-Be "OpenTelemetry.Instrumentation.Foo"
        $title | Should-Be "Component.proj for OpenTelemetry.Instrumentation.Foo"
        $project | Should-Be "./build/Projects/Component.proj"
    }
}

Describe "ResolveProjectForTag" {

    It "uses the solution build when no tag is provided" {
        $title = $null
        $project = $null
        $component = $null

        ResolveProjectForTag -tag "" -title ([ref]$title) -project ([ref]$project) -component ([ref]$component)

        $title | Should-Be "opentelemetry-dotnet-contrib.proj"
        $project | Should-Be "opentelemetry-dotnet-contrib.proj"
    }

    It "resolves a tag to a matching build project file" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $buildProjects = Join-Path -Path $work -ChildPath "build/Projects"
        New-Item -Path $buildProjects -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $buildProjects -ChildPath "Foo.proj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"

        $title = $null
        $project = $null
        $component = $null

        Push-Location -Path $work -ErrorAction Stop
        try {
            ResolveProjectForTag -tag "foo-1.0.0" -title ([ref]$title) -project ([ref]$project) -component ([ref]$component)
        }
        finally {
            Pop-Location
        }

        $title | Should-Be "Foo.proj"
        $project | Should-Be "./build/Projects/Foo.proj"
    }

    It "falls back to the shared component build for a matching csproj" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        # An empty build/Projects directory so the first lookup finds nothing.
        New-Item -Path (Join-Path -Path $work -ChildPath "build/Projects") -ItemType Directory -Force | Out-Null

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>comp-</MinVerTagPrefix></PropertyGroup></Project>"

        $title = $null
        $projectPath = $null
        $component = $null

        Push-Location -Path $work -ErrorAction Stop
        try {
            ResolveProjectForTag -tag "comp-1.0.0" -title ([ref]$title) -project ([ref]$projectPath) -component ([ref]$component)
        }
        finally {
            Pop-Location
        }

        $component | Should-Be "OpenTelemetry.Instrumentation.Foo"
        $title | Should-Be "Component.proj for OpenTelemetry.Instrumentation.Foo"
        $projectPath | Should-Be "./build/Projects/Component.proj"
    }

    It "throws when the tag cannot be parsed" {
        $title = $null
        $project = $null
        $component = $null

        { ResolveProjectForTag -tag "notag" -title ([ref]$title) -project ([ref]$project) -component ([ref]$component) } |
            Should-Throw -ExceptionMessage "*Could not parse prefix or version from tag*"
    }

    It "throws when no project matches the tag prefix" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path (Join-Path -Path $work -ChildPath "build/Projects") -ItemType Directory -Force | Out-Null

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Other"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Other.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>other-</MinVerTagPrefix></PropertyGroup></Project>"

        $title = $null
        $projectPath = $null
        $component = $null

        Push-Location -Path $work -ErrorAction Stop
        try {
            { ResolveProjectForTag -tag "missing-1.0.0" -title ([ref]$title) -project ([ref]$projectPath) -component ([ref]$component) } |
                Should-Throw -ExceptionMessage "*No project file found matching tag prefix*"
        }
        finally {
            Pop-Location
        }
    }
}

Describe "FindComponentOwners" {

    It "returns the component owners and does not leak the result of HashSet.Add" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Instrumentation.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Instrumentation.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"

        $componentOwnersDirectory = Join-Path -Path $work -ChildPath ".github"
        New-Item -Path $componentOwnersDirectory -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $componentOwnersDirectory -ChildPath "component_owners.yml") -Value @"
components:
  src/OpenTelemetry.Instrumentation.Foo/:
    - owner1
    - owner2
  src/OpenTelemetry.Instrumentation.Bar/:
    - owner3
"@

        $componentOwners = $null

        Push-Location -Path $work -ErrorAction Stop
        try {
            $result = FindComponentOwners `
                -component "OpenTelemetry.Instrumentation.Foo" `
                -componentOwners ([ref]$componentOwners)
        }
        finally {
            Pop-Location
        }

        # If the [void] cast on $componentOwners.Value.Add(...) is ever removed
        # the boolean results of Add() leak into the output stream and the
        # function returns more than the single expected $true value.
        @($result).Count | Should-Be 1 -Because "the boolean from HashSet.Add must not leak into the output"
        $result | Should-BeTrue

        $componentOwners.Contains("owner1") | Should-BeTrue
        $componentOwners.Contains("owner2") | Should-BeTrue
    }

    It "comments and returns false when the component project file is missing" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path (Join-Path -Path $work -ChildPath "src") -ItemType Directory -Force | Out-Null

        Mock -CommandName "gh" -ModuleName "build" -MockWith { }

        $componentOwners = $null

        Push-Location -Path $work -ErrorAction Stop
        try {
            $result = FindComponentOwners `
                -component "OpenTelemetry.DoesNotExist" `
                -issueNumber "123" `
                -componentOwners ([ref]$componentOwners)
        }
        finally {
            Pop-Location
        }

        $result | Should-BeFalse
        Should -Invoke -CommandName "gh" -ModuleName "build" -Exactly -Times 1 -ParameterFilter {
            $args -contains "issue" -and $args -contains "comment"
        } -Because "the requester should be told the component project could not be found"
    }

    It "returns false when the project file has no MinVerTagPrefix" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.NoPrefix"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.NoPrefix.csproj") `
            -Value "<Project><PropertyGroup></PropertyGroup></Project>"

        Mock -CommandName "gh" -ModuleName "build" -MockWith { }

        $componentOwners = $null

        Push-Location -Path $work -ErrorAction Stop
        try {
            $result = FindComponentOwners `
                -component "OpenTelemetry.NoPrefix" `
                -issueNumber "123" `
                -componentOwners ([ref]$componentOwners)
        }
        finally {
            Pop-Location
        }

        $result | Should-BeFalse
    }
}
