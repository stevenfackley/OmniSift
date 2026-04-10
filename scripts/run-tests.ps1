Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    Write-Host "Running OmniSift unit tests..."
    dotnet test tests/OmniSift.UnitTests/OmniSift.UnitTests.csproj -f net10.0

    Write-Host "Running OmniSift integration tests..."
    dotnet test tests/OmniSift.IntegrationTests/OmniSift.IntegrationTests.csproj -f net10.0
}
finally {
    Pop-Location
}
