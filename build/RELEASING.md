
# Release Process

Releasing of projects from the opentelemetry-dotnet-contrib repo is
currently a mix of automated workflow and some manual steps. Hopefully
most of this will be automated soon.

## Pre-requisites

To carry out the release successfully, make sure you fulfill the following:

1. You must be a maintainer on the repo. This will allow you to push tags to
`main` branch and create a GitHub release later on.

2. Have access to the Nuget token for releasing the project to Nuget.org
OpenTelemetry account.

## Pre-steps

1. Decide on the version to use for the release.

2. Update the Changelog for your project with relevant details.
Replace any "Unreleased" heading with this version and add the release date.

3. If you are releasing stable version, update public API definition
in `PublicAPI.Shipped.txt` files and cleanup corresponding `PublicAPI.Unshipped.txt`.

4. Submit a PR to update Changelog and get it merged to `main` branch.

## Steps

*Note:* Before starting with the following steps, ensure that the latest commit
on the `main` branch is the one which added/updated the Changelog to
the project being released. *This latest commit will be tagged on the release.*

1. Create and push git tag for the project and the version of the project
you want to release. The version should be the one used in the **Pre-steps** to
update the Changelog.

    ```powershell
    git tag -a <PROJECT TAG PREFIX>-<VERSION> -m "<PROJECT TAG PREFIX>-<VERSION>"
    ```

    You can find the project tag prefix in the `.csproj` file for the project.
    Look for value of the `<MinVerTagPrefix>` tag.
    See the example [here](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/22f2eb1026162510571241eae1eb6c2952146ace/src/OpenTelemetry.Contrib.Instrumentation.AWS/OpenTelemetry.Contrib.Instrumentation.AWS.csproj#L6).

    ```powershell
    git push origin <PROJECT TAG PREFIX>-<VERSION>
    ```

    **example:**

    ```powershell
    git tag -a Instrumentation.AWS-1.0.0 -m "Instrumentation.AWS-1.0.0"
    git push origin Instrumentation.AWS-1.0.0
    ```

    **Note:** If you are releasing more than one inter-dependent projects
    that need to go out together, you should create and push all the relevant
    tags together. This will ensure that [MinVer](https://github.com/adamralph/minver#how-it-works)
    will pick up the version for each project is what you specified in the
    tags instead of calculating the version based on the tag depth in the commit
    history.

    This will trigger the building and packaging workflow for the project.

2. Navigate to the
[**Actions**](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/actions)
tab on the repo and monitor the "Pack YOUR_PROJECT_NAME" workflow. The last step
in the workflow is to publish to Nuget.org and prepare corresponding github
release page.

3. Validate that the new version (as specified in step 1) of the project is
successfully published to nuget.org under OpenTelemetry owner.

4. Validate that the new version was published in GitHub.
