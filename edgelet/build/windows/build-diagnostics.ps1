# Copyright (c) Microsoft. All rights reserved.

<#
 # Builds and publishes to target/publish/ the iotedge-diagnostics binary and its associated dockerfile
 #>

param(
    [ValidateSet('debug', 'release')]
    [string]$BuildConfiguration = 'release'
)
 
$ErrorActionPreference = 'Continue'

. (Join-Path $PSScriptRoot 'util.ps1')

Assert-Rust
If ($BuildConfiguration -eq 'release') {
    $BuildConfiguration = 'release'
    $BuildConfigOption = '--release'
} else {
    $BuildConfiguration = 'debug'
    $BuildConfigOption = ''
}

$cargo = Get-CargoCommand
$ManifestPath = Get-Manifest

$versionInfoFilePath = Join-Path $env:BUILD_REPOSITORY_LOCALPATH 'versionInfo.json'
$env:VERSION = Get-Content $versionInfoFilePath | ConvertFrom-JSON | % version
$env:NO_VALGRIND = 'true'

$originalRustflags = $env:RUSTFLAGS
$env:RUSTFLAGS += ' -C target-feature=+crt-static'
Write-Host "$cargo build -p iotedge-diagnostics $BuildConfigOption --manifest-path $ManifestPath"
Invoke-Expression "$cargo build -p iotedge-diagnostics $BuildConfigOption --manifest-path $ManifestPath"
if ($originalRustflags -eq '') {
    Remove-Item Env:\RUSTFLAGS
}
else {
    $env:RUSTFLAGS = $originalRustflags
}
if ($LastExitCode) {
    Throw "cargo build failed with exit code $LastExitCode"
}

$ErrorActionPreference = 'Stop'

$publishFolder = [IO.Path]::Combine($env:BUILD_BINARIESDIRECTORY, 'publish', 'azureiotedge-diagnostics')

New-Item -Type Directory $publishFolder

Copy-Item -Recurse `
    ([IO.Path]::Combine($env:BUILD_REPOSITORY_LOCALPATH, 'edgelet', 'iotedge-diagnostics', 'docker')) `
    ([IO.Path]::Combine($publishFolder, 'docker'))

Copy-Item `
    ([IO.Path]::Combine($env:BUILD_REPOSITORY_LOCALPATH, 'edgelet', 'target', $BuildConfiguration, 'iotedge-diagnostics.exe')) `
    ([IO.Path]::Combine($publishFolder, 'docker', 'windows', 'amd64'))
