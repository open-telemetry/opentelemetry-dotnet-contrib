$SCRIPT_DIR = $PSScriptRoot
$ROOT_DIR = "${SCRIPT_DIR}/../"

# freeze the spec version to make SemanticAttributes generation reproducible
$SPEC_VERSION = "1.25.0"
$SCHEMA_URL = "https://opentelemetry.io/schemas/$SPEC_VERSION"
$GENERATOR_VERSION = "latest"

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
  -v ${SCRIPT_DIR}/semantic-conventions/model:/source `
  -v ${SCRIPT_DIR}/templates:/templates `
  -v ${ROOT_DIR}:/output `
  otel/semconvgen:$GENERATOR_VERSION `
  -f /source code `
  --template /templates/SemanticConventionsAttributes.cs.j2 `
  --output /output/SemanticConventionsAttributes.cs `
  --trim-whitespace `
  -D schemaUrl=$SCHEMA_URL `
