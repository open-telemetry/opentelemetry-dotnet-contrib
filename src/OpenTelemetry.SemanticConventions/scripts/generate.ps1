$SCRIPT_DIR = $PSScriptRoot
$ROOT_DIR = "${SCRIPT_DIR}/../"

# freeze the spec & generator tools versions to make SemanticAttributes generation reproducible
$SPEC_VERSION = "1.13.0"
$SCHEMA_URL = "https://opentelemetry.io/schemas/$SPEC_VERSION"
$GENERATOR_VERSION = "0.16.0"

Set-Location $SCRIPT_DIR

Remove-Item -r -fo semantic-conventions
mkdir semantic-conventions
Set-Location semantic-conventions

git init
git remote add origin https://github.com/open-telemetry/opentelemetry-specification.git
git fetch origin v$SPEC_VERSION
git reset --hard FETCH_HEAD
Set-Location ${SCRIPT_DIR}

docker run --rm `
  -v ${SCRIPT_DIR}/semantic-conventions/semantic_conventions/logs:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Logs:/output `
  otel/semconvgen:$GENERATOR_VERSION `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/LogsSemanticConventions.cs `
  --trim-whitespace `
  -D class=LogsSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Logs

docker run --rm `
  -v ${SCRIPT_DIR}/semantic-conventions/semantic_conventions/resource:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Resource:/output `
  otel/semconvgen:$GENERATOR_VERSION `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/ResourceSemanticConventions.cs `
  --trim-whitespace `
  -D class=ResourceSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Resources

docker run --rm `
  -v ${SCRIPT_DIR}/semantic-conventions/semantic_conventions/trace:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Trace:/output `
  otel/semconvgen:$GENERATOR_VERSION `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/TraceSemanticConventions.cs `
  --trim-whitespace `
  -D class=TraceSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Trace
