function ResolveProjectForTag {
  param(
    [Parameter()][string]$tag
  )

  if ([string]::IsNullOrEmpty($tag))
  {
      # Scheduled builds. No tag, build solution.
      echo "title=opentelemetry-dotnet-contrib.proj" >> $env:GITHUB_OUTPUT
      echo "project=opentelemetry-dotnet-contrib.proj" >> $env:GITHUB_OUTPUT
  }
  else {
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

        echo "title=$buildProject.proj" >> $env:GITHUB_OUTPUT
        echo "project=./build/Projects/$buildProject.proj" >> $env:GITHUB_OUTPUT
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

      $project = [System.IO.Path]::GetFileNameWithoutExtension($projects[0].Path)

      echo "title=Component.proj[$project]" >> $env:GITHUB_OUTPUT
      echo "project=./build/Projects/Component.proj" >> $env:GITHUB_OUTPUT
      echo "BUILD_COMPONENT=$project" >> $env:GITHUB_ENV
  }
}

Export-ModuleMember -Function ResolveProjectForTag

function ResolveProject {
  param(
    [Parameter(Mandatory=$true)][string]$projectName
  )

  $match = [regex]::Match($projectName, '^Component\[(.*)\]$')
  if ($match.Success -eq $false)
  {
    echo "title=$projectName.proj" >> $env:GITHUB_OUTPUT
    echo "project=./build/Projects/$projectName.proj" >> $env:GITHUB_OUTPUT
    Return
  }

  $project = $match.Groups[1].Value

  echo "title=Component.proj[$project]" >> $env:GITHUB_OUTPUT
  echo "project=./build/Projects/Component.proj" >> $env:GITHUB_OUTPUT
  echo "BUILD_COMPONENT=$project" >> $env:GITHUB_ENV
}

Export-ModuleMember -Function ResolveProject
