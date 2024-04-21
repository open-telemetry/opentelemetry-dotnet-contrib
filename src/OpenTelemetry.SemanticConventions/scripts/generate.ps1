$SCRIPT_DIR = $PSScriptRoot
$ROOT_DIR = "${SCRIPT_DIR}/../"

# freeze the spec & generator tools versions to make SemanticAttributes generation reproducible
$SPEC_VERSION = "1.25.0"
$SCHEMA_URL = "https://opentelemetry.io/schemas/$SPEC_VERSION"

Set-Location $SCRIPT_DIR

Remove-Item -r -fo semantic-conventions
mkdir semantic-conventions
Set-Location semantic-conventions

git init
git remote add origin https://github.com/open-telemetry/semantic-conventions.git
git fetch origin v$SPEC_VERSION
git reset --hard FETCH_HEAD
Set-Location ${SCRIPT_DIR}

docker run --rm `
  -v ${SCRIPT_DIR}/semantic-conventions/model/trace:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Trace:/output `
  otel/semconvgen:latest `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/TraceSemanticConventions.cs `
  --trim-whitespace `
  -D class=TraceSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Trace

docker run --rm `
  -v ${SCRIPT_DIR}/semantic-conventions/model/resource:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}/Resource:/output `
  otel/semconvgen:latest `
  -f /source code `
  --template /templates/SemanticConventions.cs.j2 `
  --output /output/ResourceSemanticConventions.cs `
  --trim-whitespace `
  -D class=ResourceSemanticConventions `
  -D schemaUrl=$SCHEMA_URL `
  -D pkg=OpenTelemetry.Resources

Set-Location ${ROOT_DIR}
