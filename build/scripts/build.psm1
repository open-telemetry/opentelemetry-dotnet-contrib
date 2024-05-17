function ResolveProjectForTag {
  param(
    [Parameter()][string]$tag,
    [Parameter()][ref]$title,
    [Parameter()][ref]$project,
    [Parameter()][ref]$component,
  )

  # Scheduled builds. No tag, build solution.
  if ([string]::IsNullOrEmpty($tag))
  {
      $title = "opentelemetry-dotnet-contrib.proj"
      $project = "opentelemetry-dotnet-contrib.proj"
      Return
  }

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  # Step 1: Look for a .proj file in build/Projects with a matching MinVerTagPrefix
  $buildProjects = @(Get-ChildItem -Path build/Projects/*.proj | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" -List | Select Path)

  if ($buildProjects.Length -gt 1)
  {
      throw 'Multiple build project files found matching tag prefix'
  }
  elseif ($buildProjects.Length -eq 1)
  {
    $buildProject = [System.IO.Path]::GetFileNameWithoutExtension($buildProjects[0].Path)

    $title = "$buildProject.proj"
    $project = "./build/Projects/$buildProject.proj"
    Return
  }

  # Step 2: If no .proj file found use component build for the csproj found matching MinVerTagPrefix
  $projects = @(Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" -List | Select Path)

  if ($projects.Length -gt 1)
  {
      throw 'Multiple project files found matching tag prefix'
  }
  elseif ($projects.Length -ne 1)
  {
      throw 'No project file found matching tag prefix'
  }

  $component = [System.IO.Path]::GetFileNameWithoutExtension($projects[0].Path)
  $title = "Component.proj for $component"
  $project = "./build/Projects/Component.proj"
}

Export-ModuleMember -Function ResolveProjectForTag

function ResolveProject {
  param(
    [Parameter(Mandatory=$true)][string]$projectNameOrComponentData,
    [Parameter()][ref]$title,
    [Parameter()][ref]$project,
    [Parameter()][ref]$component,
  )

  $match = [regex]::Match($projectNameOrComponentData, '^Component\[(.*)\]$')
  if ($match.Success -eq $false)
  {
    $title = "$projectNameOrComponentData.proj"
    $project = "./build/Projects/$projectNameOrComponentData.proj"
    Return
  }

  $component = $match.Groups[1].Value
  $title = "Component.proj for $component"
  $project = "./build/Projects/Component.proj"
}

Export-ModuleMember -Function ResolveProject
