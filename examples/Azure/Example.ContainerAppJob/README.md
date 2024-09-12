# Example.ContainerAppJob

This example demonstrates the working of the `AzureContainerAppsResourceDetector`
in the context of an Azure Container Apps job.

It is intended to run the container image in an Azure Container Apps job.
It expects the `AZURE_APPINSIGHTS_CONNECTION_STRING` environment variable
to be set with the connection string of an Azure Application Insights resource.

## Building it

To build this app, run the following command from the project directory:

```shell
dotnet publish -c Release -r linux-x64 -p:PublishProfile=DefaultContainer
```

Upload it to your container registry:

```shell
az acr login --name <container-registry-name>
docker tag opentelemetry-containerapp-job <container-registry-name>.azurecr.io/opentelemetry-containerapp-job
docker push <container-registry-name>.azurecr.io/opentelemetry-containerapp-job
```

## Prerequisites

- Resources in Azure:

  - Azure Container Apps job
  - Azure Container Registry
  - Azure Application Insights

- Building the code requires
  - adding the `InternalsVisibleTo` attribute in `AssemblyInfo.cs`
    in the the `OpenTelemetry.Resources.Azure` project because the
    `AzureContainerAppsResourceDetector` class is marked `internal`.

    ```csharp
    [assembly: InternalsVisibleTo("Example.ContainerAppJob")]
    ```

  - running docker or podman on the machine
