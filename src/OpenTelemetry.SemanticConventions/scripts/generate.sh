#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="$(cd "${ROOT_DIR}/.." && pwd)"

# Freeze the spec and generator versions to make SemanticAttributes generation reproducible.
SEMCONV_VERSION="1.41.0"
SEMCONV_COMMIT="e018fe6f91862f5ed63c082f87697cddac596784"
GENERATOR_VERSION="v0.23.0"

SEMCONV_REPO_DIR="${SCRIPT_DIR}/semantic-conventions"
STABLE_OUTPUT_DIR="${SRC_DIR}/OpenTelemetry.SemanticConventions"
INCUBATING_OUTPUT_DIR="${SRC_DIR}/OpenTelemetry.SemanticConventions.Incubating"

cd "${SCRIPT_DIR}"

rm -rf "${SEMCONV_REPO_DIR}"
mkdir "${SEMCONV_REPO_DIR}"
cd "${SEMCONV_REPO_DIR}"

git init
git remote add origin https://github.com/open-telemetry/semantic-conventions.git
git fetch --depth 1 origin "${SEMCONV_COMMIT}"
git fetch --depth 1 origin "refs/tags/v${SEMCONV_VERSION}"

TAG_COMMIT="$(git rev-list -n 1 FETCH_HEAD)"
if [ "${TAG_COMMIT}" != "${SEMCONV_COMMIT}" ]; then
  echo "semantic-conventions v${SEMCONV_VERSION} resolved to ${TAG_COMMIT}, expected ${SEMCONV_COMMIT}."
  exit 1
fi

git checkout --detach "${SEMCONV_COMMIT}"
cd "${SCRIPT_DIR}"

rm -rf \
  "${STABLE_OUTPUT_DIR}/Attributes" \
  "${STABLE_OUTPUT_DIR}/SchemaUrl.cs" \
  "${STABLE_OUTPUT_DIR}/SchemaUrl.g.cs" \
  "${STABLE_OUTPUT_DIR}/SchemaVersion.cs" \
  "${STABLE_OUTPUT_DIR}/SchemaVersion.g.cs" \
  "${INCUBATING_OUTPUT_DIR}/Attributes" \
  "${INCUBATING_OUTPUT_DIR}/SchemaUrl.cs" \
  "${INCUBATING_OUTPUT_DIR}/SchemaUrl.g.cs" \
  "${INCUBATING_OUTPUT_DIR}/SchemaVersion.cs" \
  "${INCUBATING_OUTPUT_DIR}/SchemaVersion.g.cs"

mkdir -p \
  "${STABLE_OUTPUT_DIR}/Attributes" \
  "${INCUBATING_OUTPUT_DIR}/Attributes"

docker run --rm \
  -v "${SEMCONV_REPO_DIR}/model:/source:ro" \
  -v "${SCRIPT_DIR}/templates:/templates:ro" \
  -v "${SRC_DIR}:/output" \
  otel/weaver:$GENERATOR_VERSION \
  registry \
  generate \
  --registry=/source \
  --templates=/templates \
  "./" \
  "/output/./"
