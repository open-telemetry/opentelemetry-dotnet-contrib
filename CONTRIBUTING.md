# Contributing to opentelemetry-dotnet-contrib

The OpenTelemetry .NET special interest group (SIG) meets regularly. See the
OpenTelemetry [community](https://github.com/open-telemetry/community#net-sdk)
repo for information on this and other language SIGs.

See the [public meeting
notes](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing)
for a summary description of past meetings. To request edit access, join the
meeting or get in touch on
[Slack](https://cloud-native.slack.com/archives/C01N3BC2W7Q).

## Find a Buddy and Get Started Quickly

If you are looking for someone to help you find a starting point and be a
resource for your first contribution, join our Slack channel and find a buddy!

1. Create your [CNCF Slack account](http://slack.cncf.io/) and join the
   [otel-dotnet](https://cloud-native.slack.com/archives/C01N3BC2W7Q) channel.
2. Post in the room with an introduction to yourself, what area you are
   interested in (check issues marked with [help
   wanted](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/labels/help%20wanted)),
   and say you are looking for a buddy. We will match you with someone who has
   experience in that area.

Your OpenTelemetry buddy is your resource to talk to directly on all aspects of
contributing to OpenTelemetry: providing context, reviewing PRs, and helping
those get merged. Buddies will not be available 24/7, but is committed to
responding during their normal contribution hours.

## Development Environment

You can contribute to this project from a Windows, macOS or Linux machine.

On all platforms, the minimum requirements are:

* Git client and command line tools.
* .NET 7.0+

Please note that individual project requirements might vary.

### Linux or MacOS

* Visual Studio for Mac or Visual Studio Code

Mono might be required by your IDE but is not required by this project. This is
because unit tests targeting .NET Framework (`net452`, `net46`, `net461` etc.)
are disabled outside of Windows.

### Windows

* Visual Studio 2022+ or Visual Studio Code
* .NET Framework 4.6.2+

## Pull Requests

### How to Send Pull Requests

Everyone is welcome to contribute code to `opentelemetry-dotnet-contrib` via
GitHub pull requests (PRs).

To create a new PR, fork the project in GitHub and clone the upstream repo:

```sh
git clone https://github.com/open-telemetry/opentelemetry-dotnet-contrib.git
```

Navigate to the repo root:

```sh
cd opentelemetry-dotnet-contrib
```

Add your fork as an origin:

```sh
git remote add fork https://github.com/YOUR_GITHUB_USERNAME/opentelemetry-dotnet-contrib.git
```

Run build:

```sh
dotnet build
```

Run tests:

```sh
dotnet test --no-build
```

If you made changes to the Markdown documents (`*.md` files), install the latest
[`markdownlint-cli`](https://github.com/igorshubovych/markdownlint-cli) and run:

```sh
markdownlint .
```

Check out a new branch, make modifications and push the branch to your fork:

```sh
$ git checkout -b feature
# edit files
$ git commit
$ git push fork feature
```

Open a pull request against the main `opentelemetry-dotnet-contrib` repo.

### How to Receive Comments

* If the PR is not ready for review, please mark it as
  [`draft`](https://github.blog/2019-02-14-introducing-draft-pull-requests/).
* Make sure CLA is signed and CI is clear.
* Submit small, focused PRs addressing a single concern/issue.
* Make sure the PR title reflects the contribution.
* Write a summary that helps understand the change.
* Include usage examples in the summary, where applicable.

### How to Get PRs Merged

A PR is considered to be **ready to merge** when:

* It has received an approval either from one of the
  [Approvers](https://github.com/open-telemetry/community/blob/master/community-membership.md#approver)
  /
  [Maintainers](https://github.com/open-telemetry/community/blob/master/community-membership.md#maintainer)
  or the respective component owner.
* Major feedbacks are resolved.
* It has been open for review for at least one working day. This gives people
  reasonable time to review.
* Trivial change (typo, cosmetic, doc, etc.) doesn't have to wait for one day.
* Urgent fix can take exception as long as it has been actively communicated.

Any Maintainer can merge the PR once it is **ready to merge**. Note that some
PR may not be merged immediately if repo is being in process of a major release
and the new feature doesn't fit it.

### How to request for release of package

* Submit a PR with `CHANGELOG.md` file reflecting the version to be released
along with the date in the following format `yyyy-MMM-dd`.

For example:

```text
## 1.2.0-beta.2

Released 2022-Jun-21
```

* Tag the maintainers of this repository
(@open-telemetry/dotnet-contrib-maintainers) who can release the package.

## Style Guide

This project includes a
[`.editorconfig`](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/master/.editorconfig)
file which is supported by all the IDEs/editor mentioned above. It works with
the IDE/editor only and does not affect the actual build of the project.

This repository also includes [stylecop ruleset
files](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/master/build).
These files are used to configure the _StyleCop.Analyzers_ which runs during
build. Breaking the rules will result in a build failure.

## Contributing a new project

This repo is a great place to contribute a new instrumentation, exporter or any
kind of extension. Please refer to [this
page](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/extending-the-sdk/README.md#extending-the-opentelemetry-net-sdk)
for help writing your component.

When contributing a new project you are expected to assign either yourself or
someone else who would take ownership of the component you are contributing. The
owner should at least be an [OpenTelemetry
Member](https://github.com/open-telemetry/community/blob/main/community-membership.md#member)
to be eligible to assigned as component owner. This is required to ensure that
reviews can be automatically requested from the owners. Once the owner is
identified, please update [component_owners](./.github/component_owners.yml)
file for the new project. The component owner(s) are expected to respond to
issues and review PRs affecting their component.

Although the projects within this repo share some properties and configurations,
they are built and released independently. So if you are creating a new project
within `/src` and corresponding test project within `/test`, here are a few
things you should do to ensure that your project is automatically built and
shipped through CI.

* Based on what your project is, you may need to depend on the [OpenTelemetry
SDK](https://www.nuget.org/packages/OpenTelemetry) or the [OpenTelemetry
API](https://www.nuget.org/packages/OpenTelemetry.Api) Include the necessary
package in your project. You can choose the version that you want to depend on.
Usually, it is a good idea to use the latest stable version. For example:

```xml
<ItemGroup>
    <PackageReference Include="OpenTelemetry" Version="$(OpenTelemetryCoreLatestVersion)" />
</ItemGroup>
```

* If your component relies on new features not yet part of the stable release, you
can refer to the latest pre-release version.

```xml
<ItemGroup>
    <PackageReference Include="OpenTelemetry" Version="$(OpenTelemetryCoreLatestPrereleaseVersion)" />
</ItemGroup>
```

* The assembly and nuget versioning is managed through
[MinVer](https://github.com/adamralph/minver) for all the projects in the repo.
MinVer will assign the version to your project based on the tag prefix specified
by you. To ensure your project is versioned appropriately, specify a
`<MinVerTagPrefix>` property in your project file. If your project is named as
"OpenTelemetry.Instrumentation.FooBar", the MinVerTagPrefix must be
"Instrumentation.FooBar-". Example:

```xml
<PropertyGroup>
    <MinVerTagPrefix>Instrumentation.FooBar-</MinVerTagPrefix>
</PropertyGroup>
```

* Public API of all packages is analyzed by
[Microsoft.CodeAnalysis.PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/Microsoft.CodeAnalysis.PublicApiAnalyzers.md).
This analyzer requires files structure to store the information about public API.
Create `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` for each `TargetFramework`
defined in csproj under `{ProjectFolder}\.publicApi\{TargetFramework}`.

* To build and release your project as nuget, you must provide a GitHub workflow
to be triggered when a tag with prefix "Instrumentation.FooBar-" is pushed to
the main branch. The workflow file should be named as
`package-Instrumentation.FooBar.yml` and to be placed in the
`.github/workflows/` folder.

  You can copy one of the [existing workflow
  files](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/.github/workflows/package-Instrumentation.AspNet.yml)
  and replace the workflow
  [`name`](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/.github/workflows/package-Instrumentation.AspNet.yml#L1)
  with "Pack OpenTelemetry.Instrumentation.FooBar",
  [`tags`](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/.github/workflows/package-Instrumentation.AspNet.yml#L12)
  with "Instrumentation.FooBar-*" and
  [`PROJECT`](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/.github/workflows/package-Instrumentation.AspNet.yml#L18)
  with "OpenTelemetry.Instrumentation.FooBar".

* Add an issue template in your PR. You can follow the existing issue templates,
  e.g. [comp_extensions](./.github/ISSUE_TEMPLATE/comp_extensions.md). The
  maintainer will help to create a new ["comp:"
  label](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/labels?q=comp%3A)
  once the PR is merged.

* Add a README file for your project describing how to install and use your
  package. Every project's README file needs to have a link to the Nuget
  package. You can use the below snippet for reference:

```md
[![NuGet version badge](https://img.shields.io/nuget/v/{your_package_name})](https://www.nuget.org/packages/{your_package_name})
[![NuGet download count badge](https://img.shields.io/nuget/dt/{your_package_name})](https://www.nuget.org/packages/{your_package_name})
```

### Guidance for components on supporting target frameworks

* SHOULD support the [.NET
  versions](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main#supported-net-versions)
  which are supported by the OpenTelemetry main repo packages.
* MUST document the target frameworks supported in the main README.md file.
* MUST NOT support out-of-support .NET runtimes (e.g.: `.NET Framework 4.5.2`,
  `.NET Core 2.1` etc). CI checks in this repository will not be run against out
  of support versions.
* Whenever a .NET version reaches end of support, components MUST drop support
  for it as well. Note: This change does not require major version bump. For
  reference see
  [this](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3351).
