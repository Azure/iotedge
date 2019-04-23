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

$ErrorActionPreference = 'Continue'

# arm build has to use a few private forks of dependencies instead of the public ones, in order to to this, we have to 
# 1. append a [patch] section in cargo.toml to use crate forks
# 2. run cargo update commands to force update cargo.lock to use the forked crates
# after these two steps, running cargo build for arm should not show any warning such as "Patch 'crate-x' was not used in the crate graph"
if($Arm)
{
    $edgefolder = Get-EdgeletFolder
    Set-Location -Path $edgefolder

    $ForkedCrates = @"

[patch.crates-io]
backtrace = { git = "https://github.com/philipktlin/backtrace-rs", branch = "arm" }
cmake = { git = "https://github.com/philipktlin/cmake-rs", branch = "arm" }
dtoa = { git = "https://github.com/philipktlin/dtoa", branch = "arm" }
iovec = { git = "https://github.com/philipktlin/iovec", branch = "arm" }
mio = { git = "https://github.com/philipktlin/mio", branch = "arm" }
miow = { git = "https://github.com/philipktlin/miow", branch = "arm" }
serde-hjson = { git = "https://github.com/philipktlin/hjson-rust", branch = "arm" }
winapi = { git = "https://github.com/philipktlin/winapi-rs", branch = "arm/v0.3.5" }

[patch."https://github.com/Azure/mio-uds-windows"]
mio-uds-windows = { git = "https://github.com/philipktlin/mio-uds-windows", branch = "arm" }

[patch."https://github.com/Azure/hyperlocal-windows"]
hyperlocal-windows = { git = "https://github.com/philipktlin/hyperlocal-windows", branch = "arm" }

[patch."https://github.com/Azure/tokio-uds-windows"]
tokio-uds-windows = { git = "https://github.com/philipktlin/tokio-uds-windows", branch = "arm" }
"@

    Write-Host "Append cargo.toml with $ForkedCrates"
    Add-Content -Path cargo.toml -Value $ForkedCrates

    Write-Host "Running cargo update to lock the crate forks required by arm build"
    Invoke-Expression "$cargo update -p https://github.com/Azure/mio-uds-windows.git#mio-uds-windows:0.1.0"
    Invoke-Expression "$cargo update -p mio --precise 0.6.14"
    Invoke-Expression "$cargo update -p https://github.com/Azure/tokio-uds-windows.git#tokio-uds-windows:0.1.0"
    Invoke-Expression "$cargo update -p winapi --precise 0.3.5"
}


# Run cargo build by specifying the manifest file

$ManifestPath = Get-Manifest

if($Arm)
{
    # arm build requires cl.exe from vc tools to expand a c file for openssl-sys
    $env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\vc\Tools\MSVC\14.16.27023\bin\Hostx64\x64;" + $env:PATH
    Write-Host $(Get-Command cl.exe).Path
}

Write-Host "$cargo build --all $(if ($Arm) {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build --all $(if ($Arm) {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"

if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'
