#!/usr/bin/env bash
# Regenerate Resources/resolved-registry.json from the pinned semconv model
# via Weaver. Mirrors contrib's scripts/generate.sh invocation pattern at
# v0.23.0 / v1.41.0 for byte-reproducibility.
#
# Prereqs:
#   - Docker installed and running.
#   - .tools/semconv-upstream submodule initialised (`git submodule update --init`).
#
# Output: <project_root>/Resources/resolved-registry.json (overwrites).
set -euo pipefail

GENERATOR_VERSION="v0.23.0"
SEMCONV_VERSION="1.41.0"
SEMCONV_COMMIT="e018fe6f91862f5ed63c082f87697cddac596784"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "${script_dir}/.." && pwd)"
repo_root="$(cd "${project_dir}/../.." && pwd)"
source_registry="${repo_root}/.tools/semconv-upstream/model"
templates_dir="${script_dir}/templates"
output_dir="${project_dir}"

if [[ ! -d "${source_registry}" ]]; then
  echo "error: ${source_registry} not found — run \`git submodule update --init .tools/semconv-upstream\`" >&2
  exit 1
fi

docker run --rm \
  -v "${source_registry}:/source" \
  -v "${templates_dir}:/templates" \
  -v "${output_dir}:/output" \
  "otel/weaver:${GENERATOR_VERSION}" \
  registry generate \
    --registry=/source \
    --templates=/templates \
    "./" \
    "/output/./"

echo "Regenerated ${output_dir}/Resources/resolved-registry.json from semconv ${SEMCONV_VERSION} (${SEMCONV_COMMIT}) via Weaver ${GENERATOR_VERSION}."
