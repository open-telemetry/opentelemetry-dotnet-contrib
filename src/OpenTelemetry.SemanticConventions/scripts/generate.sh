#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="${SCRIPT_DIR}/../"

# freeze the spec version to make SemanticAttributes generation reproducible
SPEC_VERSION=1.25.0
GENERATOR_VERSION=latest

cd ${SCRIPT_DIR}

rm -rf semantic-conventions || true
mkdir semantic-conventions
cd semantic-conventions

git init
git remote add origin https://github.com/open-telemetry/semantic-conventions.git
git fetch origin "v$SPEC_VERSION"
git reset --hard FETCH_HEAD
cd ${SCRIPT_DIR}

docker run --rm \
  -v ${SCRIPT_DIR}/semantic-conventions/model:/source \
  -v ${SCRIPT_DIR}/templates:/templates \
  -v ${ROOT_DIR}/Attributes:/output \
  otel/semconvgen:$GENERATOR_VERSION \
  -f /source code \
  --template /templates/SemanticConventionsAttributes.cs.j2 \
  --output /output/{{pascal_prefix}}Attributes.cs \
  --trim-whitespace \
  --file-per-group root_namespace
