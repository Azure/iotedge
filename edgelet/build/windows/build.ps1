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

# arm build has to use a few private forks of dependencies instead of the public ones, in order to to this, we have to 
# 1. append a [patch] section in cargo.toml
# 2. restore a preconfigured cargo.lock to use these dependencies
if($Arm)
{
    $edgefolder = Get-EdgeletFolder
    Set-Location -Path $edgefolder

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

    Add-Content -Path cargo.toml -Value $ForkedCrates
}

# Run cargo build by specifying the manifest file
$cargo = Get-CargoCommand -Arm:$Arm

$ManifestPath = Get-Manifest

Write-Host "$cargo build $(if (-Not $Arm) { '--all'} else {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
Invoke-Expression "$cargo build $(if (-Not $Arm) { '--all'} else {'--target thumbv7a-pc-windows-msvc'}) $(if ($Release) { '--release' }) --manifest-path $ManifestPath"
if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}
