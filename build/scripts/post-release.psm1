$gitHubBotUserName="github-actions[bot]"
$gitHubBotEmail="41898282+github-actions[bot]@users.noreply.github.com"

$repoViewResponse = gh repo view --json nameWithOwner | ConvertFrom-Json

$gitRepository = $repoViewResponse.nameWithOwner

function CreateRelease {
  param(
    [Parameter(Mandatory=$true)][string]$tag
  )

  $packages = (Get-ChildItem -Path src/*/bin/Release/*.nupkg).Name

  $notes = ''
  $firstPackageVersion = ''

  foreach ($package in $packages)
  {
      $match = [regex]::Match($package, '(.*)\.(\d+\.\d+\.\d+.*?)\.nupkg')
      $packageName = $match.Groups[1].Value
      $packageVersion = $match.Groups[2].Value

      if ($firstPackageVersion -eq '')
      {
          $firstPackageVersion = $packageVersion
      }

      $changelogContent = Get-Content -Path "src/$packageName/CHANGELOG.md"

      $started = $false
      $content = ""

      foreach ($line in $changelogContent)
      {
          if ($line -like "## $packageVersion" -and $started -ne $true)
          {
              $started = $true
          }
          elseif ($line -like "Released *" -and $started -eq $true)
          {
              continue
          }
          elseif ($line -like "## *" -and $started -eq $true)
          {
              break
          }
          else
          {
              if ($started -eq $true -and ([string]::IsNullOrWhitespace($line) -eq $false -or $content.Length -gt 0))
              {
                  $content += "  " + $line + "`r`n"
              }
          }
      }

      if ([string]::IsNullOrWhitespace($content) -eq $true)
      {
          $content = "   No notable changes."
      }

      $content = $content.trimend()

      $notes +=
@"
* NuGet: [$packageName v$packageVersion](https://www.nuget.org/packages/$packageName/$packageVersion)

$content

  See [CHANGELOG](https://github.com/$gitRepository/blob/$tag/src/$packageName/CHANGELOG.md) for details.

"@
  }

  if ($firstPackageVersion -match '-alpha' -or $firstPackageVersion -match '-beta' -or $firstPackageVersion -match '-rc')
  {
    gh release create $tag `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --prerelease
  }
  else
  {
    gh release create $tag `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --latest
  }
}

Export-ModuleMember -Function CreateRelease

function CreatePackageValidationBaselineVersionUpdatePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$gitUserName=$gitHubBotUserName,
    [Parameter()][string]$gitUserEmail=$gitHubBotEmail,
    [Parameter()][string]$targetBranch="main"
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  $branch="release/post-stable-${tag}-update"

  git config user.name $gitUserName
  git config user.email $gitUserEmail

  git switch --create $branch origin/$targetBranch --no-track 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  $versionTagRegex = "<PackageValidationBaselineVersion>.*</PackageValidationBaselineVersion>"

  $projects = Get-ChildItem -Path src/**/*.csproj |
    where { $_ | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" } |
    where { $_ | Select-String $versionTagRegex }

  if ($projects.Length -ne 0)
  {
    foreach ($project in $projects) {
      (Get-Content $project) `
          -replace $versionTagRegex, "<PackageValidationBaselineVersion>$version</PackageValidationBaselineVersion>" |
        Set-Content $project

      git add $project 2>&1 | % ToString
      if ($LASTEXITCODE -gt 0)
      {
          throw 'git add failure'
      }
    }
  }

  $diabledTag = "<DisablePackageBaselineValidation>true</DisablePackageBaselineValidation>"

  $disabledProjects = Get-ChildItem -Path src/**/*.csproj |
    where { $_ | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" } |
    where { $_ | Select-String $diabledTag }

  if ($disabledProjects.Length -ne 0)
  {
    foreach ($project in $disabledProjects) {
      $content = (Get-Content $project -Raw)

      $content = $content -replace $diabledTag, "<PackageValidationBaselineVersion>$version</PackageValidationBaselineVersion>"

      $content = $content -replace "<!--\s*?Do not run Package Baseline Validation[\S\s]*?-->\s*", ""

      Set-Content $project -Value $content.TrimEnd()

      git add $project 2>&1 | % ToString
      if ($LASTEXITCODE -gt 0)
      {
          throw 'git add failure'
      }
    }
  }

  git commit -m "Update PackageValidationBaselineVersion in $tagPrefix projects to $version." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  $body =
@"
Note: This PR was opened automatically by the [package workflow](https://github.com/$gitRepository/actions/workflows/publish-packages.yml).

Merge once packages are available on NuGet and the build passes.

## Changes

* Sets ``PackageValidationBaselineVersion`` in ``$tagPrefix`` projects to ``$version``.
"@

  gh pr create `
    --title "[repo] $tagPrefix stable release $version updates" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label infra
}

Export-ModuleMember -Function CreatePackageValidationBaselineVersionUpdatePullRequest

function CreateOpenTelemetryCoreLatestVersionUpdatePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$gitUserName=$gitHubBotUserName,
    [Parameter()][string]$gitUserEmail=$gitHubBotEmail,
    [Parameter()][string]$targetBranch="main"
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  if ($tagPrefix.StartsWith('core-') -eq $false)
  {
    Return
  }

  $projectsAndDependenciesBefore = GetCoreDependenciesForProjects

  $version = $match.Groups[2].Value
  $isPrerelease = ($version.Contains('-alpha.') -or $version.Contains('-beta.') -or $version.Contains('-rc.'))

  $branch="release/post-core-${version}-update"

  $propertyName = "OpenTelemetryCoreLatestVersion"
  $propertyVersion = "[$version,2.0)"
  if ($isPrerelease -eq $true)
  {
   $propertyName = "OpenTelemetryCoreLatestPrereleaseVersion"
   $propertyVersion = "[$version]"
  }

  (Get-Content build/Common.props) `
      -replace "<$propertyName>.*<\/$propertyName>", "<$propertyName>$propertyVersion</$propertyName>" |
    Set-Content build/Common.props

  $projectsAndDependenciesAfter = GetCoreDependenciesForProjects

  $changedProjects = @{}

  $projectsAndDependenciesBefore.GetEnumerator() | ForEach-Object {
    $projectDir = $_.Key
    $projectDependenciesBefore = $_.Value
    $projectDependenciesAfter = $projectsAndDependenciesAfter[$projectDir]

    $projectDependenciesBefore.GetEnumerator() | ForEach-Object {
      $packageName = $_.Key
      $packageVersionBefore = $_.Value
      if ($projectDependenciesAfter[$packageName] -ne $packageVersionBefore)
      {
          $changedProjects[$projectDir] = $true
      }
    }
  }

  git config user.name $gitUserName
  git config user.email $gitUserEmail

  git switch --create $branch origin/$targetBranch --no-track 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  git add build/Common.props 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git add failure'
  }

  git commit -m "Update $propertyName in Common.props to $version." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  $body =
@"
Note: This PR was opened automatically by the [core version update workflow](https://github.com/$gitRepository/actions/workflows/core-version-update.yml).

Merge once packages are available on NuGet and the build passes.

## Changes

* Sets ``$propertyName`` in ``Common.props`` to ``$version``.
"@

  $createPullRequestResponse = gh pr create `
    --title "[repo] Core release $version updates" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label infra

  $match = [regex]::Match($createPullRequestResponse, "\/pull\/(.*)$")
  if ($match.Success -eq $false)
  {
      throw 'Could not parse pull request number from gh pr create response'
  }

  $pullRequestNumber = $match.Groups[1].Value

  if ($changedProjects.Count -eq 0)
  {
    Return
  }

$entry = @"
* Updated OpenTelemetry core component version(s) to ``$version``.
  ([#$pullRequestNumber](https://github.com/$gitRepository/pull/$pullRequestNumber))


"@

  $lastLineBlank = $true

  foreach ($projectDir in $changedProjects.Keys)
  {
      $path = Join-Path -Path $projectDir -ChildPath "CHANGELOG.md"

      $changelogContent = Get-Content -Path $path

      $started = $false
      $isRemoving = $false
      $content = ""

      foreach ($line in $changelogContent)
      {
          if ($line -like "## Unreleased" -and $started -ne $true)
          {
              $started = $true
          }
          elseif ($line -like "## *" -and $started -eq $true)
          {
              if ($lastLineBlank -eq $false)
              {
                  $content += "`r`n"
              }
              $content += $entry
              $started = $false
              $isRemoving = $false
          }
          elseif ($line -like '*Update* OpenTelemetry SDK version to*' -and $started -eq $true)
          {
              $isRemoving = $true
              continue
          }

          if ($line.StartsWith('* '))
          {
              if ($isRemoving -eq $true)
              {
                  $isRemoving = $false
              }

              if ($lastLineBlank -eq $false)
              {
                  $content += "`r`n"
              }
          }

          if ($isRemoving -eq $true)
          {
              continue
          }

          $content += $line + "`r`n"

          $lastLineBlank = [string]::IsNullOrWhitespace($line)
      }

      if ($started -eq $true)
      {
        # Note: If we never wrote the entry it means the file ended in the unreleased section
        if ($lastLineBlank -eq $false)
        {
            $content += "`r`n"
        }
        $content += $entry
      }

      Set-Content -Path $path -Value $content.TrimEnd()

      git add $path 2>&1 | % ToString
      if ($LASTEXITCODE -gt 0)
      {
          throw 'git add failure'
      }
  }

  git commit -m "Update CHANGELOGs for projects using $propertyName." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }
}

Export-ModuleMember -Function CreateOpenTelemetryCoreLatestVersionUpdatePullRequest

function GetCoreDependenciesForProjects {
    $projects = @(Get-ChildItem -Path 'src/*/*.csproj')

    $projectsAndDependencies = @{}

    foreach ($project in $projects)
    {
        # Note: dotnet restore may fail if the core packages aren't available yet but that is fine, we just want to generate project.assets.json for these projects.
        $output = dotnet restore $project

        $projectDir = $project | Split-Path -Parent

        $content = (Get-Content "$projectDir/obj/project.assets.json" -Raw)

        $projectDependencies = @{}

        $matches = [regex]::Matches($content, '"(OpenTelemetry(?:.*))?": {[\S\s]*?"target": "Package",[\S\s]*?"version": "(.*)"[\S\s]*?}')
        foreach ($match in $matches)
        {
            $packageName = $match.Groups[1].Value
            $packageVersion = $match.Groups[2].Value
            if ($packageName -eq 'OpenTelemetry' -or
                $packageName -eq 'OpenTelemetry.Api' -or
                $packageName -eq 'OpenTelemetry.Api.ProviderBuilderExtensions' -or
                $packageName -eq 'OpenTelemetry.Extensions.Hosting')
            {
                $projectDependencies[$packageName.ToString()] = $packageVersion.ToString()
            }
        }
        $projectsAndDependencies[$projectDir.ToString()] = $projectDependencies
    }

    return $projectsAndDependencies
}
