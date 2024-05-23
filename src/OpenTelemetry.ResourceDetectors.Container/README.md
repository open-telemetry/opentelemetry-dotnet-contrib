# Container Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.Container)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Container)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.Container)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Container)

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetectors.Container` package to be able to use the
Container Resource Detectors.

```shell
dotnet add package OpenTelemetry.ResourceDetectors.Container --prerelease
```

## Usage

You can configure Container resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Container;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource
                            .AddDetector(new ContainerResourceDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **ContainerResourceDetector**: container.id.

## Kubernetes

When running in a Kubernetes environment, the Container resource detector
requires permissions to access pod information in order
to extract the container ID.
This is achieved using Kubernetes Role-Based Access Control (RBAC).

You will need to create a Role and RoleBinding to grant
the necessary permissions to the service account used by your application.
Below is an example of how to configure these RBAC resources:

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
```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
