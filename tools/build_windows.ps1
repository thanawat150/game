param(
    [Parameter(Mandatory = $true)]
    [string]$GodotPath
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$BuildDirectory = Join-Path $ProjectRoot "builds\windows"
$ArtifactPath = Join-Path $BuildDirectory "WorldForgePixelGods.exe"

if (-not (Test-Path $GodotPath)) {
    throw "Godot executable was not found: $GodotPath"
}

New-Item -ItemType Directory -Force -Path $BuildDirectory | Out-Null

Push-Location $ProjectRoot
try {
    & dotnet test ".\tests\WorldForge.Core.Tests\WorldForge.Core.Tests.csproj" -c Release
    if ($LASTEXITCODE -ne 0) { throw "Core tests failed with code $LASTEXITCODE" }

    & $GodotPath --headless --path $ProjectRoot --build-solutions --quit
    if ($LASTEXITCODE -ne 0) { throw "Godot C# build failed with code $LASTEXITCODE" }

    & $GodotPath --headless --path $ProjectRoot --export-release "Windows Desktop" $ArtifactPath
    if ($LASTEXITCODE -ne 0) { throw "Godot export failed with code $LASTEXITCODE" }

    if (-not (Test-Path $ArtifactPath)) { throw "Expected artifact was not created: $ArtifactPath" }
    Write-Host "Build passed: $ArtifactPath"
}
finally {
    Pop-Location
}
