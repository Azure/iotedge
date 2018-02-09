# Copyright (c) Microsoft. All rights reserved.

param(
    [switch]$Release
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

# Ensure rust is installed
Assert-Rust

# Run cargo build by specifying the manifest file
$cargo = Get-CargoCommand
$ManifestPath = Get-Manifest

Write-Host "$cargo build --all $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build --all $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}