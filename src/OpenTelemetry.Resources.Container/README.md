# Container Resource Detectors

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@iskiselev](https://github.com/iskiselev)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Container)](https://www.nuget.org/packages/OpenTelemetry.Resources.Container)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Container)](https://www.nuget.org/packages/OpenTelemetry.Resources.Container)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Container)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Container)

## Getting Started

You need to install the
`OpenTelemetry.Resources.Container` package to be able to use the
Container Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.Container --prerelease
```

## Usage

You can configure Container resource detector to
the `ResourceBuilder` with the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources.Container;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddContainerDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddContainerDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddContainerDetector());
    });
});
```

The resource detectors will record the following metadata based on where
your application is running:

- **ContainerDetector**: container.id.

## Kubernetes

To make container ID resolution work, container and pod name should be provided
through `KUBERNETES_CONTAINER_NAME` and `KUBERNETES_POD_NAME` environment variable
respectively and pod should have at least
get permission to kubernetes resource pods.
It can be done by utilizing YAML anchoring, downwards API
and RBAC (Role-Based Access Control).

If `KUBERNETES_POD_NAME` is not provided, detector will use `HOSTNAME`
as a fallback, but it may not work in some environments
or if hostname was overridden in pod spec.

Below is an example of how to configure sample pod
to make container ID resolution working:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: pod-reader-account
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  namespace: default
  name: pod-reader
rules:
- apiGroups: [""]
  resources: ["pods"]
  verbs: ["get"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: read-pods
  namespace: default
subjects:
- kind: ServiceAccount
  name: pod-reader-account
  namespace: default
roleRef:
  kind: Role
  name: pod-reader
  apiGroup: rbac.authorization.k8s.io
---
apiVersion: v1
kind: Pod
metadata:
  name: container-resolve-demo
spec:
  serviceAccountName: pod-reader-account
  volumes:
  - name: shared-data
    emptyDir: {}
  containers:
  - name: &container_name my_container_name
    image: ubuntu:latest
    command: [ "/bin/bash", "-c", "--" ]
    args: [ "while true; do sleep 30; done;" ]
    env:
    - name: KUBERNETES_CONTAINER_NAME
      value: *container_name
    - name: KUBERNETES_POD_NAME
      valueFrom:
        fieldRef:
          fieldPath: metadata.name
```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
