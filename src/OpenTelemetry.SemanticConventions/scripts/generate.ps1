$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$SCRIPT_DIR = $PSScriptRoot
$ROOT_DIR = (Resolve-Path (Join-Path $SCRIPT_DIR "..")).Path
$SRC_DIR = (Resolve-Path (Join-Path $ROOT_DIR "..")).Path

# Freeze the spec and generator versions to make SemanticAttributes generation reproducible.
$SEMCONV_VERSION="1.41.0"
$SEMCONV_COMMIT="e018fe6f91862f5ed63c082f87697cddac596784"
$GENERATOR_VERSION="v0.23.0"

Set-Location $SCRIPT_DIR

$SEMCONV_REPO_DIR = Join-Path $SCRIPT_DIR "semantic-conventions"
$STABLE_OUTPUT_DIR = Join-Path $SRC_DIR "OpenTelemetry.SemanticConventions"
$INCUBATING_OUTPUT_DIR = Join-Path $SRC_DIR "OpenTelemetry.SemanticConventions.Incubating"

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $SEMCONV_REPO_DIR
New-Item -ItemType Directory -Path $SEMCONV_REPO_DIR | Out-Null
Set-Location $SEMCONV_REPO_DIR

git init
git remote add origin https://github.com/open-telemetry/semantic-conventions.git
git fetch --depth 1 origin $SEMCONV_COMMIT
git fetch --depth 1 origin "refs/tags/v$SEMCONV_VERSION"

$TAG_COMMIT = git rev-list -n 1 FETCH_HEAD
if ($TAG_COMMIT -ne $SEMCONV_COMMIT) {
  Write-Error "semantic-conventions v$SEMCONV_VERSION resolved to $TAG_COMMIT, expected $SEMCONV_COMMIT."
}

git checkout --detach $SEMCONV_COMMIT
Set-Location ${SCRIPT_DIR}

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
  (Join-Path $STABLE_OUTPUT_DIR "Attributes"), `
  (Join-Path $STABLE_OUTPUT_DIR "SchemaUrl.cs"), `
  (Join-Path $STABLE_OUTPUT_DIR "SchemaUrl.g.cs"), `
  (Join-Path $STABLE_OUTPUT_DIR "SchemaVersion.cs"), `
  (Join-Path $STABLE_OUTPUT_DIR "SchemaVersion.g.cs"), `
  (Join-Path $INCUBATING_OUTPUT_DIR "Attributes"), `
  (Join-Path $INCUBATING_OUTPUT_DIR "SchemaUrl.cs"), `
  (Join-Path $INCUBATING_OUTPUT_DIR "SchemaUrl.g.cs"), `
  (Join-Path $INCUBATING_OUTPUT_DIR "SchemaVersion.cs"), `
  (Join-Path $INCUBATING_OUTPUT_DIR "SchemaVersion.g.cs")

New-Item -ItemType Directory -Path (Join-Path $STABLE_OUTPUT_DIR "Attributes") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $INCUBATING_OUTPUT_DIR "Attributes") -Force | Out-Null

docker run --rm `
  -v ${SEMCONV_REPO_DIR}/model:/source:ro `
  -v ${SCRIPT_DIR}/templates:/templates:ro `
  -v ${SRC_DIR}:/output `
  otel/weaver:$GENERATOR_VERSION `
  registry `
  generate `
  --registry=/source `
  --templates=/templates `
  "./" `
  "/output/./"`
