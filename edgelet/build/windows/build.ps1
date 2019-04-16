# Copyright (c) Microsoft. All rights reserved.

param(
    [switch]$Release,
    [switch]$Arm
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

# currently arm rust is private tool chain on the build machine with fixed path
if(-Not $Arm)
{
    # Ensure rust is installed
    Assert-Rust
}

# Run cargo build by specifying the manifest file
$cargo = Get-CargoCommand -Arm:$Arm
$ManifestPath = Get-Manifest

Write-Host "OPENSSL_DIR $env:OPENSSL_DIR"
Write-Host "OPENSSL_ROOT_DIR $env:OPENSSL_ROOT_DIR"

Write-Host "$cargo build $(if (-Not $Arm) { '--all'} else {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build $(if (-Not $Arm) { '--all'} else {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}
