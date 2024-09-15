function CreatePullRequestToUpdateChangelogsAndPublicApis {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$component,
    [Parameter(Mandatory=$true)][string]$version,
    [Parameter(Mandatory=$true)][string]$requestedByUserName,
    [Parameter()][string]$releaseIssue,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $match = [regex]::Match($version, '^(\d+\.\d+\.\d+)(?:-((?:alpha)|(?:beta)|(?:rc))\.(\d+))?$')
  if ($match.Success -eq $false)
  {
      throw 'Input version did not match expected format'
  }

  $projectContent = Get-Content -Path src/$component/$component.csproj

  $match = [regex]::Match($projectContent, '<MinVerTagPrefix>(.*)<\/MinVerTagPrefix>')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse MinVerTagPrefix from project file'
  }

  $minVerTagPrefix = $match.Groups[1].Value
  $tag="${minVerTagPrefix}${version}"
  $branch="release/prepare-${tag}-release"

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch --create $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  if ([string]::IsNullOrEmpty($releaseIssue) -eq $false)
  {
    $issueText =
@"

Release request: #$releaseIssue
"@
  }

  $body =
@"
Note: This PR was opened automatically by the [prepare release workflow](https://github.com/$gitRepository/actions/workflows/prepare-release.yml).
$issueText
Requested by: @$requestedByUserName

## Changes

* CHANGELOG files updated for projects being released.
"@

  # Update CHANGELOGs
  & ./build/scripts/update-changelogs.ps1 -minVerTagPrefix $minVerTagPrefix -version $version

  # Update publicApi files for stable releases
  if ($version -notlike "*-alpha*" -and $version -notlike "*-beta*" -and $version -notlike "*-rc*")
  {
    & ./build/scripts/finalize-publicapi.ps1 -minVerTagPrefix $minVerTagPrefix

    $body += "`r`n* Public API files updated for projects being released (only performed for stable releases)."
  }

  $body +=
@"

## Commands

``/UpdateReleaseDates``: Use to update release dates in CHANGELOGs before merging [``approvers``, ``maintainers``]
``/CreateReleaseTag``: Use after merging to push the release tag and trigger the job to create packages and push to NuGet [``approvers``, ``maintainers``]
"@

  git commit -a -m "Prepare repo to release $tag." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  gh pr create `
    --title "[release] Prepare release $tag" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label release
}

Export-ModuleMember -Function CreatePullRequestToUpdateChangelogsAndPublicApis

function LockPullRequestAndPostNoticeToCreateReleaseTag {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName
  )

  $prViewResponse = gh pr view $pullRequestNumber --json mergeCommit,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $commit = $prViewResponse.mergeCommit.oid
  if ([string]::IsNullOrEmpty($commit) -eq $true)
  {
      throw 'Could not find merge commit'
  }

  $body =
@"
I noticed this PR was merged.

Post a comment with "/CreateReleaseTag" in the body if you would like me to create the release tag ``$tag`` for [the merge commit](https://github.com/$gitRepository/commit/$commit) which will trigger the package workflow.
"@

  gh pr comment $pullRequestNumber --body $body

  gh pr lock $pullRequestNumber
}

Export-ModuleMember -Function LockPullRequestAndPostNoticeToCreateReleaseTag

function CreateReleaseTagAndPostNoticeOnPullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $prViewResponse = gh pr view $pullRequestNumber --json mergeCommit,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $commit = $prViewResponse.mergeCommit.oid
  if ([string]::IsNullOrEmpty($commit) -eq $true)
  {
      throw 'Could not find merge commit'
  }

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git tag -a $tag -m "$tag" $commit 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git tag failure'
  }

  git push origin $tag 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  gh pr unlock $pullRequestNumber

  $body =
@"
I just pushed the [$tag](https://github.com/$gitRepository/releases/tag/$tag) tag.

The [package workflow](https://github.com/$gitRepository/actions/workflows/publish-packages.yml) should begin momentarily.
"@

  gh pr comment $pullRequestNumber --body $body
}

Export-ModuleMember -Function CreateReleaseTagAndPostNoticeOnPullRequest

function UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter(Mandatory=$true)][string]$commentUserName,
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $prViewResponse = gh pr view $pullRequestNumber --json headRefName,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  $commentUserPermission = gh api "repos/$gitRepository/collaborators/$commentUserName/permission" | ConvertFrom-Json
  if ($commentUserPermission.permission -ne 'admin' -and $commentUserPermission.permission -ne 'write')
  {
    gh pr comment $pullRequestNumber `
      --body "I'm sorry @$commentUserName but you don't have permission to update this PR. Only maintainers and approvers can update this PR."
    return
  }

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch $prViewResponse.headRefName 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  $updatedFiles = 0
  $newHeader =
@"
## $version

Released $(Get-Date -UFormat '%Y-%b-%d')
"@

  $projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" -List | Select Path | Split-Path -Parent

  foreach ($projectDir in $projectDirs)
  {
    $content = (Get-Content "$projectDir/CHANGELOG.md" -Raw)

    $newContent = $content -replace "## $version\s*Released .*", $newHeader

    if ($content -ne $newContent)
    {
      $updatedFiles++
      Set-Content -Path "$projectDir/CHANGELOG.md" $newContent.Trim()
    }
  }

  if ($updatedFiles -eq 0)
  {
    gh pr comment $pullRequestNumber --body "All of the CHANGELOG files have valid release dates."
    return
  }

  git commit -a -m "Update CHANGELOG release dates for $tag." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $prViewResponse.headRefName 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  gh pr comment $pullRequestNumber --body "I updated the CHANGELOG release dates."
}

Export-ModuleMember -Function UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest

function TagCodeOwnersOnOrRunWorkflowForRequestReleaseIssue {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$triggeringEventName,
    [Parameter(Mandatory=$true)][string]$requestedByUserName,
    [Parameter(Mandatory=$true)][string]$issueNumber,
    [Parameter(Mandatory=$true)][string]$issueBody
  )

  $match = [regex]::Match($issueBody, '^[#]+ Component\s*(OpenTelemetry\.(?:.|\w+)+)$')
  if ($match.Success -eq $false)
  {
      Return
  }

  $component = $match.Groups[1].Value

  $match = [regex]::Match($issueBody, '^[#]+ Version\s*(.*)$')
  if ($match.Success -eq $false)
  {
      Return
  }

  $version = $match.Groups[1].Value
  $versionMatch = [regex]::Match($version, '^(\d+\.\d+\.\d+)(?:-((?:alpha)|(?:beta)|(?:rc))\.(\d+))?$')

  $requestedByUserPermission = gh api "repos/$gitRepository/collaborators/$requestedByUserName/permission" | ConvertFrom-Json

  $projectContent = Get-Content -Path src/$component/$component.csproj

  $match = [regex]::Match($projectContent, '<MinVerTagPrefix>(.*)<\/MinVerTagPrefix>')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse MinVerTagPrefix from project file'
  }

  $minVerTagPrefix = $match.Groups[1].Value

  $projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$minVerTagPrefix</MinVerTagPrefix>" -List | Select Path | Split-Path -Parent

  $componentOwnersContent = Get-Content '.github/component_owners.yml' -Raw

  $componentOwners = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

  foreach ($projectDir in $projectDirs)
  {
    $projectName = [System.IO.Path]::GetFileName($projectDir)

    $match = [regex]::Match($componentOwnersContent, "src\/$projectName\/:([\w\W\s]*?)src")
    if ($match.Success -eq $true)
    {
      $matches = [regex]::Matches($match.Groups[1].Value, "-\s*(.*)")
      foreach ($match in $matches)
      {
        $owner = $match.Groups[1].Value
        $_ = $componentOwners.Add($owner.Trim())
      }
    }
  }

  $kickOffWorkflow = $false
  $kickOffWorkflowReason = ''

  if ($requestedByUserPermission.permission -eq 'admin' -or $requestedByUserPermission.permission -eq 'write')
  {
    $kickOffWorkflow = $true
    $kickOffWorkflowReason = "@$requestedByUserName has collaborator or greater permission"
  }
  elseif ($componentOwners.Contains($requestedByUserName) -eq $true)
  {
    $kickOffWorkflow = $true
    $kickOffWorkflowReason = "@$requestedByUserName is a component owner"
  }

  if ($kickOffWorkflow -eq $true)
  {
    if ($versionMatch.Success -eq $false)
    {
      gh issue comment $issueNumber `
        --body "$kickOffWorkflowReason but I can't proceed to kick off the prepare release workflow because the version requested is invalid. Please creare a new issue or edit the issue description, set a valid version, and then post a comment with `"/PrepareRelease`" in the body to restart the process."
    }
    else
    {
      gh workflow run "prepare-release.yml" `
        --repo $gitRepository `
        --ref "main" `
        --field "component=$component" `
        --field "version=$version" `
        --field "releaseIssue=$issueNumber" `
        --field "requestedByUserName=$requestedByUserName"

      gh issue close $issueNumber `
        --comment "I kicked off the prepare release workflow because $kickOffWorkflowReason."
    }

    return
  }

  if ($requestedByUserName -eq 'issues')
  {
    # Executed when issues are created
    $componentOwners = ''
    if ($componentOwners.Count -gt 0)
    {
      foreach ($componentOwner in $componentOwners)
      {
        $componentOwners += "@$componentOwner "
      }
    }

    if ($versionMatch.Success -eq $false) {
      $body =
@"
$componentOwners@open-telemetry/dotnet-approvers @open-telemetry/dotnet-maintainers

It looks like a release has been requested for an invalid version ``$version``.

Please create a new issue or edit the issue description, set a valid version, and then post a comment with "/PrepareRelease" in the body if you would like me to kick off the prepare release workflow for the component listed in the issue description.
"@
    }
    else {
      $body =
@"
$componentOwners@open-telemetry/dotnet-approvers @open-telemetry/dotnet-maintainers

Post a comment with "/PrepareRelease" in the body if you would like me to kick off the prepare release workflow for the component and version listed in the issue description.
"@
    }

    gh issue comment $issueNumber --body $body
  }
  else {
    # Executed when issues are commented
    if ($kickOffWorkflow -eq $false)
    {
      gh issue comment $issueNumber `
        --body "I'm sorry @$requestedByUserPermission but you don't have permission to kick off the prepare release workflow. Only maintainers, approvers, and/or owners of the component may use the `"/PrepareRelease`" command"
    }
  }
}

Export-ModuleMember -Function TagCodeOwnersOnOrRunWorkflowForRequestReleaseIssue
