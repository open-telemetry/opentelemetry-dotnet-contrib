function CreateRelease {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$releaseFiles
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  $projects = @(Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" -List | Select Path)

  $notes = ''

  foreach ($project in $projects)
  {
      $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Path)

      $changelogContent = Get-Content -Path "src/$projectName/CHANGELOG.md"

      $started = $false
      $content = ""

      foreach ($line in $changelogContent)
      {
          if ($line -like "## $version" -and $started -ne $true)
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
* NuGet: [$projectName v$version](https://www.nuget.org/packages/$projectName/$version)

$content

  See [CHANGELOG](https://github.com/$gitRepository/blob/$tag/src/$projectName/CHANGELOG.md) for details.

"@
  }

  if ($version -match '-alpha' -or $version -match '-beta' -or $version -match '-rc')
  {
    gh release create $tag $releaseFiles `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --prerelease
  }
  else
  {
    gh release create $tag $releaseFiles `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --latest
  }
}

Export-ModuleMember -Function CreateRelease

function TryPostPackagesReadyNoticeOnPrepareReleasePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter(Mandatory=$true)][string]$tagSha,
    [Parameter(Mandatory=$true)][string]$packagesUrl,
    [Parameter(Mandatory=$true)][string]$botUserName
  )

  $prListResponse = gh pr list --search $tagSha --state merged --json number,author,title,comments | ConvertFrom-Json

  if ($prListResponse.Length -eq 0)
  {
    Write-Host 'No prepare release PR found for tag & commit skipping post notice'
    return
  }

  foreach ($pr in $prListResponse)
  {
    if ($pr.author.login -ne $botUserName -or $pr.title -ne "[release] Prepare release $tag")
    {
      continue
    }

    $foundComment = $false
    foreach ($comment in $pr.comments)
    {
      if ($comment.author.login -eq $botUserName -and $comment.body.StartsWith("I just pushed the [$tag]"))
      {
        $foundComment = $true
        break
      }
    }

    if ($foundComment -eq $false)
    {
      continue
    }

  $body =
@"
The [packages]($packagesUrl) for [$tag](https://github.com/$gitRepository/releases/tag/$tag) should be available on NuGet momentarily.

Have a nice day!
"@

    $pullRequestNumber = $pr.number

    gh pr comment $pullRequestNumber --body $body
    return
  }

  Write-Host 'No prepare release PR found matched author and title with a valid comment'
}

Export-ModuleMember -Function TryPostPackagesReadyNoticeOnPrepareReleasePullRequest

function CreatePackageValidationBaselineVersionUpdatePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  $branch="release/post-stable-${tag}-update"

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

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

  $disabledTag = "<DisablePackageBaselineValidation>true</DisablePackageBaselineValidation>"

  $disabledProjects = Get-ChildItem -Path src/**/*.csproj |
    where { $_ | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" } |
    where { $_ | Select-String $disabledTag }

  if ($disabledProjects.Length -ne 0)
  {
    foreach ($project in $disabledProjects) {
      $content = (Get-Content $project -Raw)

      $content = $content -replace $disabledTag, "<PackageValidationBaselineVersion>$version</PackageValidationBaselineVersion>"

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
Note: This PR was opened automatically by the [post-release workflow](https://github.com/$gitRepository/actions/workflows/post-release.yml).

Merge once packages are available on NuGet and the build passes.

## Changes

* Sets ``PackageValidationBaselineVersion`` in ``$tagPrefix`` projects to ``$version``.
"@

  gh pr create `
    --title "[release] $tagPrefix stable release $version updates" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label release
}

Export-ModuleMember -Function CreatePackageValidationBaselineVersionUpdatePullRequest

function CreateOpenTelemetryCoreLatestVersionUpdatePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$lineEnding="`n",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value
  $isPrerelease = ($version.Contains('-alpha.') -or $version.Contains('-beta.') -or $version.Contains('-rc.'))

  if ($tagPrefix.StartsWith('core-') -eq $true)
  {
    $changelogEntry = "Updated OpenTelemetry core component version(s) to"
    $propertyName = "OpenTelemetryCoreLatestVersion"
    $propertyVersion = "[$version,2.0)"
    if ($isPrerelease -eq $true)
    {
      $propertyName = "OpenTelemetryCoreLatestPrereleaseVersion"
      $propertyVersion = "[$version]"
    }
  }
  elseif ($tagPrefix.StartsWith('coreunstable-') -eq $true)
  {
    $changelogEntry = "Updated OpenTelemetry core unstable component version(s) to"
    $propertyName = "OpenTelemetryCoreUnstableLatestVersion"
    $propertyVersion = "[$version]"
  }
  else
  {
    Return
  }

  $branch="release/post-core-${version}-update"

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch --create $branch origin/$targetBranch --no-track 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  $projectsAndDependenciesBefore = GetCoreDependenciesForProjects

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
    --title "[release] $tag release updates" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label release

  Write-Host $createPullRequestResponse

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
* $changelogEntry ``$version``.
  ([#$pullRequestNumber](https://github.com/$gitRepository/pull/$pullRequestNumber))


"@

  $lastLineBlank = $true
  $changelogFilesUpdated = 0

  foreach ($projectDir in $changedProjects.Keys)
  {
      $path = Join-Path -Path $projectDir -ChildPath "CHANGELOG.md"

      if ([System.IO.File]::Exists($path) -eq $false)
      {
        Write-Host "No CHANGELOG found in $projectDir"
        continue
      }

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
                  $content += $lineEnding
              }
              $content += $entry
              $started = $false
              $isRemoving = $false
          }
          elseif ($started -eq $true -and $tagPrefix.StartsWith('core-') -eq $true -and $line -like '*Update* OpenTelemetry SDK version to*')
          {
            $isRemoving = $true
            continue
          }
          elseif ($started -eq $true -and $line -like "*$changelogEntry*")
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
                  $content += $lineEnding
              }
          }

          if ($isRemoving -eq $true)
          {
              continue
          }

          $content += $line + $lineEnding

          $lastLineBlank = [string]::IsNullOrWhitespace($line)
      }

      if ($started -eq $true)
      {
        # Note: If we never wrote the entry it means the file ended in the unreleased section
        if ($lastLineBlank -eq $false)
        {
            $content += $lineEnding
        }
        $content += $entry
      }

      Set-Content -Path $path -Value $content.TrimEnd()

      git add $path 2>&1 | % ToString
      if ($LASTEXITCODE -gt 0)
      {
          throw 'git add failure'
      }

      $changelogFilesUpdated++
  }

  if ($changelogFilesUpdated -gt 0)
  {
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
                $packageName -eq 'OpenTelemetry.Extensions.Hosting' -or
                $packageName -eq 'OpenTelemetry.Extensions.Propagators' -or
                $packageName -eq 'OpenTelemetry.Exporter.Prometheus.AspNetCore' -or
                $packageName -eq 'OpenTelemetry.Exporter.Prometheus.HttpListener' -or
                $packageName -eq 'OpenTelemetry.Shims.OpenTracing')
            {
                $projectDependencies[$packageName.ToString()] = $packageVersion.ToString()
            }
        }
        $projectsAndDependencies[$projectDir.ToString()] = $projectDependencies
    }

    return $projectsAndDependencies
}
