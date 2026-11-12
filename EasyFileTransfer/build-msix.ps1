param(
    [ValidateSet("Store", "Sideload")]
    [string]$Mode = "Store",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootScript = Join-Path (Split-Path -Parent $scriptDir) "build-msix-easyfiletransfer.ps1"

if (Test-Path $rootScript) {
    & $rootScript -Mode $Mode -Configuration $Configuration -Platform $Platform
} else {
    Write-Error "Root script not found at $rootScript"
    exit 1
}
