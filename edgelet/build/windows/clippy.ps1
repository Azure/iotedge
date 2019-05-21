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

rustup component add clippy

if ($LastExitCode -ne 0) {
    Throw "Unable to install clippy. Failed with $LastExitCode"
}

$oldPath = if ($Arm) { ReplacePrivateRustInPath } else { '' }

$ErrorActionPreference = 'Continue'

if ($Arm) {
    PatchRustForArm -OpenSSL
}

# Run cargo build by specifying the manifest file

$ManifestPath = Get-Manifest

Write-Host "$cargo clippy --all --manifest-path $ManifestPath"
Invoke-Expression "$cargo clippy --all --manifest-path $ManifestPath"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --all failed with exit code $LastExitCode"
}

Write-Host "$cargo clippy --all --tests --manifest-path $ManifestPath"
Invoke-Expression "$cargo clippy --all --tests --manifest-path $ManifestPath"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --all --tests failed with exit code $LastExitCode"
}

Write-Host "$cargo clippy --all --examples --manifest-path $ManifestPath"
Invoke-Expression "$cargo clippy --all --examples --manifest-path $ManifestPath"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --all --examples failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'

if ($Arm -and (-not [string]::IsNullOrEmpty($oldPath))) {
    $env:PATH = $oldPath
}
