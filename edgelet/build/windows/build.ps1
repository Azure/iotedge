# Copyright (c) Microsoft. All rights reserved.

param(
    [switch] $Release,
    [switch] $Arm
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

# Ensure rust is installed
Assert-Rust -Arm:$Arm

$cargo = Get-CargoCommand -Arm:$Arm
Write-Host $cargo

$oldPath = if ($Arm) { ReplacePrivateRustInPath } else { '' }

$ErrorActionPreference = 'Continue'

if ($Arm) {
    PatchRustForArm -OpenSSL
}

# Run cargo build by specifying the manifest file

$ManifestPath = Get-Manifest

Write-Host "$cargo build --all $(if ($Arm) { '--target thumbv7a-pc-windows-msvc' }) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build --all $(if ($Arm) { '--target thumbv7a-pc-windows-msvc' }) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"

if ($LastExitCode -ne 0) {
    Throw "cargo build failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'

if ($Arm -and (-not [string]::IsNullOrEmpty($oldPath))) {
    $env:PATH = $oldPath
}
