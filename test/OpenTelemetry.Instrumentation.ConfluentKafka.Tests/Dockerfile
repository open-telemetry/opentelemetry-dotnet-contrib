# Create a container for running the OpenTelemetry ConfluentKafka integration tests.
# This should be run from the root of the repo:
# docker build --file test/OpenTelemetry.Instrumentation.ConfluentKafka.Tests/Dockerfile .

ARG BUILD_SDK_VERSION=9.0
ARG TEST_SDK_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:8.0.415@sha256:f2f0cb3af991eb6959c8a20551b0152f10cce61354c089dd863a7b72c0f00fea AS dotnet-sdk-8.0
FROM mcr.microsoft.com/dotnet/sdk:9.0.306@sha256:ca77338a19f87a7d24494a3656cb7d878a040c158621337b9cd3ab811c5eb057 AS dotnet-sdk-9.0

FROM dotnet-sdk-${BUILD_SDK_VERSION} AS build
ARG PUBLISH_CONFIGURATION=Release
ARG PUBLISH_FRAMEWORK=net9.0
WORKDIR /repo
COPY . ./
WORKDIR "/repo/test/OpenTelemetry.Instrumentation.ConfluentKafka.Tests"
RUN dotnet publish "OpenTelemetry.Instrumentation.ConfluentKafka.Tests.csproj" -c "${PUBLISH_CONFIGURATION}" -f "${PUBLISH_FRAMEWORK}" -o /drop -p:IntegrationBuild=true

FROM dotnet-sdk-${TEST_SDK_VERSION} AS final
WORKDIR /test
COPY --from=build /drop .
ENTRYPOINT ["dotnet", "vstest", "OpenTelemetry.Instrumentation.ConfluentKafka.Tests.dll"]
