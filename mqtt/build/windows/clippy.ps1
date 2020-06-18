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

cd (Get-MqttFolder)

rustup component add clippy

if ($LastExitCode -ne 0) {
    Throw "Unable to install clippy. Failed with $LastExitCode"
}

$oldPath = if ($Arm) { ReplacePrivateRustInPath } else { '' }

$ErrorActionPreference = 'Continue'

if ($Arm) {
    PatchRustForArm -OpenSSL
}

Write-Host "$cargo clippy --all"
Invoke-Expression "$cargo clippy --all"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --workspace failed with exit code $LastExitCode"
}

Write-Host "$cargo clippy --workspace --tests --all-features"
Invoke-Expression "$cargo clippy --workspace --tests --all-features"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --workspace --tests failed with exit code $LastExitCode"
}

Write-Host "$cargo clippy --workspace --examples"
Invoke-Expression "$cargo clippy --workspace --examples"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --workspace --examples failed with exit code $LastExitCode"
}

Write-Host "$cargo clippy --workspace --benches"
Invoke-Expression "$cargo clippy --workspace --benches"

if ($LastExitCode -ne 0) {
    Throw "cargo clippy --workspace --benches failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'

if ($Arm -and (-not [string]::IsNullOrEmpty($oldPath))) {
    $env:PATH = $oldPath
}
