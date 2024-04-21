#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="${SCRIPT_DIR}/../"

# freeze the spec & generator tools versions to make SemanticAttributes generation reproducible
SPEC_VERSION=1.25.0
SCHEMA_URL=https://opentelemetry.io/schemas/$SPEC_VERSION

cd ${SCRIPT_DIR}

rm -rf semantic-conventions || true
mkdir semantic-conventions
cd semantic-conventions

git init
git remote add origin https://github.com/open-telemetry/semantic-conventions.git
git fetch origin "v$SPEC_VERSION"
git reset --hard FETCH_HEAD
cd ${SCRIPT_DIR}
rm semantic-conventions/model/trace/cloudevents.yaml
docker run --rm \
  -v ${SCRIPT_DIR}/semantic-conventions/model/trace:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/Trace:/output \
  otel/semconvgen:latest \
  -f /source code \
  --template /templates/SemanticConventions.cs.j2 \
  --output /output/TraceSemanticConventions.cs \
  --trim-whitespace \
  -Dclass=TraceSemanticConventions \
  -DschemaUrl=$SCHEMA_URL \
  -Dpkg=OpenTelemetry.Trace

docker run --rm \
  -v ${SCRIPT_DIR}/semantic-conventions/model/resource:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/Resource:/output \
  otel/semconvgen:latest \
  -f /source code \
  --template /templates/SemanticConventions.cs.j2 \
  --output /output/ResourceSemanticConventions.cs \
  --trim-whitespace \
  -Dclass=ResourceSemanticConventions \
  -DschemaUrl=$SCHEMA_URL \
  -Dpkg=OpenTelemetry.Resources
