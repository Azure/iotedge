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
$ManifestPath = Get-Manifest

$env:OPENSSL_ROOT_DIR = "C:\\vcpkg\\packages\\openssl_x64-windows"
Write-Host "OpenSSL Root Dir $env:OPENSSL_ROOT_DIR"

$env:IOTEDGE_HOMEDIR = $env:Temp

Write-Host "$cargo test --all $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo test --all $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
if ($LastExitCode)
{
    Throw "cargo test failed with exit code $LastExitCode"
}
