
$rootDirectory = Get-Location

$PowerShellScript = "$($rootDirectory)\src\OpenTelemetry.SemanticConventions\scripts\generate.ps1"
$BashScript = "$($rootDirectory)\src\OpenTelemetry.SemanticConventions\scripts\generate.sh"

# PS semconv version
$PsSemConvVersion = Select-String -Path $PowerShellScript -Pattern '\$SEMCONV_VERSION="([^"]+)"' | ForEach-Object { $_.Matches.Groups[1].Value }
# PS weaver version
$PsWeaverVersion = Select-String -Path $PowerShellScript -Pattern '\$GENERATOR_VERSION="([^"]+)"' | ForEach-Object { $_.Matches.Groups[1].Value }

# Bash semconv version
$BashSemConvVersion = Select-String -Path $BashScript -Pattern 'SEMCONV_VERSION="([^"]+)"' | ForEach-Object { $_.Matches.Groups[1].Value }
# Bash weaver version
$BashWeaverVersion = Select-String -Path $BashScript -Pattern 'SEMCONV_VERSION="([^"]+)"' | ForEach-Object { $_.Matches.Groups[1].Value }

# Check if the semconv versions match
if ($PsSemConvVersion -ne $BashSemConvVersion) {
    Write-Host "Semantic Convention version mismatch detected!"
    Write-Host "PowerShell version: $PsSemConvVersion"
    Write-Host "Bash version: $BashSemConvVersion"
    exit 1
}

# Check if the weaver versions match
if ($PsWeaverVersion -ne $BashWeaverVersion) {
    Write-Host "Weaver version mismatch detected!"
    Write-Host "PowerShell version: $PsWeaverVersion"
    Write-Host "Bash version: $BashWeaverVersion"
    exit 1
}