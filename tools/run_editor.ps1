param(
    [Parameter(Mandatory = $true)]
    [string]$GodotPath
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if (-not (Test-Path $GodotPath)) {
    throw "Godot executable was not found: $GodotPath"
}

& $GodotPath --editor --path $ProjectRoot
if ($LASTEXITCODE -ne 0) {
    throw "Godot editor exited with code $LASTEXITCODE"
}
