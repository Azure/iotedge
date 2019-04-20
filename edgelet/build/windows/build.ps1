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

# arm build has to use a few private forks of dependencies instead of the public ones, in order to to this, we have to 
# 1. append a [patch] section in cargo.toml
# 2. restore a preconfigured cargo.lock to use these dependencies
if($Arm)
{
    $edgefolder = Get-EdgeletFolder
    Set-Location -Path $edgefolder

    Write-Host "Overwrite Cargo.lock with Cargo.WinArm.lock"
    cmd /c copy /Y Cargo.WinArm.lock Cargo.lock

    $ForkedCrates = @"

[patch.crates-io]
backtrace = { git = "https://github.com/chandde/backtrace-rs", branch = "arm" }
cmake = { git = "https://github.com/philipktlin/cmake-rs", branch = "arm" }
dtoa = { git = "https://github.com/philipktlin/dtoa", branch = "arm" }
iovec = { git = "https://github.com/philipktlin/iovec", branch = "arm" }
mio = { git = "https://github.com/chandde/mio", branch = "arm" }
miow = { git = "https://github.com/philipktlin/miow", branch = "arm" }
serde-hjson = { git = "https://github.com/philipktlin/hjson-rust", branch = "arm" }
winapi = { git = "https://github.com/philipktlin/winapi-rs", branch = "arm/v0.3.5" }

[patch."https://github.com/Azure/mio-uds-windows"]
mio-uds-windows = { git = "https://github.com/philipktlin/mio-uds-windows", branch = "arm" }

[patch."https://github.com/Azure/hyperlocal-windows"]
hyperlocal-windows = { git = "https://github.com/chandde/hyperlocal-windows", branch = "arm" }

[patch."https://github.com/Azure/tokio-uds-windows"]
tokio-uds-windows = { git = "https://github.com/chandde/tokio-uds-windows", branch = "arm" }
"@

    Write-Host "Appen cargo.toml with $ForkedCrates"
    Add-Content -Path cargo.toml -Value $ForkedCrates
}

# Run cargo build by specifying the manifest file
$cargo = Get-CargoCommand -Arm:$Arm

$ManifestPath = Get-Manifest

$ErrorActionPreference = 'Continue'

if($Arm)
{
    # arm build requires cl.exe from vc tools to expand a c file for openssl-sys
    $env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\vc\Tools\MSVC\14.16.27023\bin\Hostx64\x64;" + $env:PATH
    Write-Host $(Get-Command cl.exe).Path
}

Write-Host "$cargo build $(if (-Not $Arm) { '--all'} else {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build $(if (-Not $Arm) { '--all'} else {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"

if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'