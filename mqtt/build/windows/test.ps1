# Copyright (c) Microsoft. All rights reserved.

param(
    [switch]$Release
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

# Ensure rust is installed
Assert-Rust

# Run cargo test by specifying the manifest file
$cargo = Get-CargoCommand

cd (Get-MqttFolder)

$env:IOTEDGE_HOMEDIR = $env:Temp

Write-Host "$cargo test --all $(if ($Release) { '--release' })"
Invoke-Expression "$cargo test --all $(if ($Release) { '--release' })"
if ($LastExitCode) {
    Throw "cargo test failed with exit code $LastExitCode"
}
