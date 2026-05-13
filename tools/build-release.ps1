# Builds a portable Kennel release zip.
#
# Usage:
#   pwsh tools\build-release.ps1               # version comes from csproj
#   pwsh tools\build-release.ps1 -Version 1.0.0
#
# Output lands in release\Kennel-v<version>.zip. Contents are intentionally
# minimal: Kennel.exe, Kennel.exe.config, README.md. User unzips anywhere
# and double-clicks Kennel.exe. No installer, no admin, no .NET to install
# (net48 ships with Windows 10/11).
#
# Re-runs are safe: any prior staging dir + zip for the same version is
# cleaned up before the build.

#requires -version 5
param(
    [string]$Version = $null
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot "UevrLauncher\UevrLauncher.csproj"
$binDir = Join-Path $repoRoot "UevrLauncher\bin\Release\net48"
$releaseDir = Join-Path $repoRoot "release"

# If no -Version, pull the value out of the csproj so we never ship a build
# whose title-bar version doesn't match the zip name.
if (-not $Version) {
    [xml]$csproj = Get-Content $proj
    $Version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    if (-not $Version) { throw "Couldn't read <Version> from $proj" }
}

$stageDir = Join-Path $releaseDir "Kennel-v$Version"
$zipPath = Join-Path $releaseDir "Kennel-v$Version.zip"

Write-Host "==> Building Kennel v$Version (Release)..."
& dotnet build $proj -c Release -p:Version=$Version --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host "==> Staging $stageDir"
if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

Copy-Item (Join-Path $binDir "Kennel.exe")        $stageDir
Copy-Item (Join-Path $binDir "Kennel.exe.config") $stageDir
Copy-Item (Join-Path $repoRoot "README.md")       $stageDir
Copy-Item (Join-Path $repoRoot "UNLICENSE")       $stageDir

Write-Host "==> Compressing $zipPath"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$stageDir\*" -DestinationPath $zipPath

$zipSize = (Get-Item $zipPath).Length
Write-Host ""
Write-Host "Release built:"
Write-Host "  $zipPath ($zipSize bytes)"
Write-Host ""
Write-Host "Contents:"
Get-ChildItem $stageDir | Select-Object Name, Length | Format-Table -AutoSize
