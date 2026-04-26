param(
    [ValidateSet("win-x64", "win-arm64")]
    [string] $Runtime = "win-x64",

    [switch] $FrameworkDependent,

    [string] $Version = "1.0.1"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "WinLive.App\WinLive.App.csproj"
$output = Join-Path $root "artifacts\publish\$Runtime"
$versionLabel = if ($Version.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) { $Version } else { "v$Version" }
$publishedExe = Join-Path $output "WinLive.App.exe"
$versionedExe = Join-Path $output "WinLive_$versionLabel.exe"

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained $selfContained `
    -o $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false

if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Expected published executable was not found: $publishedExe"
}

Move-Item -LiteralPath $publishedExe -Destination $versionedExe -Force

Write-Host "Published to $output"
Write-Host "Executable: $versionedExe"
