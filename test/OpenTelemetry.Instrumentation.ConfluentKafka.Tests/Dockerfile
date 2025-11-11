# Create a container for running the OpenTelemetry ConfluentKafka integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.ConfluentKafka.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=10.0
ARG TEST_SDK_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.415@sha256:4632e98dc703311ca09d0c37170fe70ad4ba334bd88a841c78cbcc9d29994b2c AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.306@sha256:81f6d622fe21ed9d31375167f62a3538ff4d6835f9d5e6da9c2defa8a84b7687 AS dotnet-sdk-9.0
FROM mcr.microsoft.com/dotnet/sdk:10.0.100@sha256:c7445f141c04f1a6b454181bd098dcfa606c61ba0bd213d0a702489e5bd4cd71 AS dotnet-sdk-10.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net10.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.ConfluentKafka.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.ConfluentKafka.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.ConfluentKafka.Tests.dll"]
