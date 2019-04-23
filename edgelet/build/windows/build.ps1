# Copyright (c) Microsoft. All rights reserved.

param(
    [switch]$Release,
    [switch]$Arm
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

# Ensure rust is installed
Assert-Rust -Arm:$Arm

$cargo = Get-CargoCommand -Arm:$Arm
Write-Host $cargo

$ErrorActionPreference = 'Continue'

# arm build has to use a few private forks of dependencies instead of the public ones, in order to to this, we have to 
# 1. append a [patch] section in cargo.toml to use crate forks
# 2. run cargo update commands to force update cargo.lock to use the forked crates
# after these two steps, running cargo build for arm should not show any warning such as "Patch 'crate-x' was not used in the crate graph"
if($Arm)
{
    PatchRustForArm
}


# Run cargo build by specifying the manifest file

$ManifestPath = Get-Manifest

Write-Host "$cargo build --all $(if ($Arm) {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build --all $(if ($Arm) {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"

if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'
