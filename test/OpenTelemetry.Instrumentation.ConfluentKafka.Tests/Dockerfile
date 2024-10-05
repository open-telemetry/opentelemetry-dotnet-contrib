# Create a container for running the OpenTelemetry ConfluentKafka integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.ConfluentKafka.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=8.0
ARG TEST_SDK_VERSION=8.0

FROM mcr.microsoft.com/dotnet/sdk:${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net8.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.ConfluentKafka.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.ConfluentKafka.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM mcr.microsoft.com/dotnet/sdk:${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.ConfluentKafka.Tests.dll"]
