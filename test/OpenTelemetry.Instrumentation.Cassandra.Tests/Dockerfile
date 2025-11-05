# Create a container for running the OpenTelemetry Cassandra integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.Cassandra.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.415@sha256:4632e98dc703311ca09d0c37170fe70ad4ba334bd88a841c78cbcc9d29994b2c AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.306@sha256:81f6d622fe21ed9d31375167f62a3538ff4d6835f9d5e6da9c2defa8a84b7687 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2@sha256:ed46c853a4f100ba7aab2394e9fb9f9b8989e93346b04632dbce11e5da7623d8 AS dotnet-sdk-10.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.Cassandra.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.Cassandra.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.Cassandra.Tests.dll"]
