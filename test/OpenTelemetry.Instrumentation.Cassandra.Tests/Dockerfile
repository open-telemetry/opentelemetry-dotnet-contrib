# Create a container for running the OpenTelemetry Cassandra integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.Cassandra.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.414@sha256:ff8311847c54c04d1a14c488362807997d59b61372da5095a95f89cbcda7f9b7 AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.305@sha256:123b43e4d9775451d8ed63af324a42132707b4edb14770bcdf5c85cf55bc45f1 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2@sha256:8e8a5bbd8dd6a6c594d6c08775fd5fc4a40b67d6bbd27882e6e8b9d5684eac97 AS dotnet-sdk-10.0

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
