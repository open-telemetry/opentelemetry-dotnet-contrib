# Create a container for running the OpenTelemetry Cassandra integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.Cassandra.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=9.0
ARG TEST_SDK_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.413@sha256:7fd287cec03e027e39be13fb06ca1347f1338c7a6ab4ad305d94d91852693f70 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.304@sha256:ae000be75dac94fc40e00f0eee903289e985995cc06dac3937469254ce5b60b6 AS dotnet-sdk-9.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net9.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.Cassandra.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.Cassandra.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.Cassandra.Tests.dll"]
