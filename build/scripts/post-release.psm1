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

      $headingWritten = $false
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
Note: This PR was opened automatically by the [package workflow](https://github.com/$gitRepository/actions/workflows/Component.Package.yml).

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
