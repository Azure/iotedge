# Copyright (c) Microsoft. All rights reserved.

<#
 # Builds and publishes to target/publish/ the iotedge-diagnostics binary and its associated dockerfile
 #>

Import-Module (Join-Path $PSScriptRoot '..' '..' '..' 'scripts' 'windows' 'Defaults.psm1') -Force

. (Join-Path $PSScriptRoot 'util.ps1')

Assert-Rust

$cargo = Get-CargoCommand
$ManifestPath = Get-Manifest

$versionInfoFilePath = Join-Path $env:BUILD_REPOSITORY_LOCALPATH 'versionInfo.json'
$env:VERSION = Get-Content $versionInfoFilePath | ConvertFrom-JSON | % version

Write-Host "$cargo build -p iotedge-diagnostics --release --manifest-path $ManifestPath"
Invoke-Expression "$cargo build -p iotedge-diagnostics --release --manifest-path $ManifestPath"
if ($LastExitCode) {
    Throw "cargo build failed with exit code $LastExitCode"
}

$buildBinariesDirectory = DefaultBuildBinariesDirectory $env:BUILD_REPOSITORY_LOCALPATH
$publishFolder = Join-Path $buildBinariesDirectory 'publish'

New-Item -Type Directory (Join-Path $publishFolder 'azureiotedge-diagnostics')

Copy-Item -Recurse `
    (Join-Path $env:BUILD_REPOSITORY_LOCALPATH 'edgelet' 'iotedge-diagnostics' 'docker') `
    (Join-Path $publishFolder 'azureiotedge-diagnostics' 'docker')

Copy-Item `
    (Join-Path $env:BUILD_REPOSITORY_LOCALPATH 'edgelet' 'target' 'release' 'iotedge-diagnostics.exe') `
    (Join-Path $publishFolder 'azureiotedge-diagnostics' 'docker' 'windows' 'amd64')
