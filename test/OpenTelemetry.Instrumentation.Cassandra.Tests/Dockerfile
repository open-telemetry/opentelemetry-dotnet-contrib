# Create a container for running the OpenTelemetry Cassandra integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.Cassandra.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=8.0
ARG TEST_SDK_VERSION=8.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.412@sha256:e6748a3b3d8cea753f643cacac0e577d8c409285b3924232db1354693bdbd3d7 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.302@sha256:3da7c4198dc77b50aeaf76f262ed0ac2ed324f87fba4b5b0f0bc0b4fbbf2ad93 AS dotnet-sdk-9.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net8.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.Cassandra.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.Cassandra.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.Cassandra.Tests.dll"]
