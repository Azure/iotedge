# Copyright (c) Microsoft. All rights reserved.

<#
 # Builds and publishes to target/publish/ the iotedge-diagnostics binary and its associated dockerfile
 #>

param(
    [ValidateSet('debug', 'release')]
    [string]$BuildConfiguration = 'release'
)


. (Join-Path $PSScriptRoot 'util.ps1')

$targetArchs = @("amd64", "arm32v7")

for ($i=0; $i -lt $targetArchs.length; $i++) {

    $Arm = ($targetArchs[$i] -eq "arm32v7")

    Assert-Rust -Arm:$Arm

    $oldPath = ""

    if($Arm)
    {
        if ($BuildConfiguration -eq "debug")
        {
            # Arm rust compiler does not support debug build
            continue
        }

        $oldPath = ReplacePrivateRustInPath

        PatchRustForArm
    }

    $ErrorActionPreference = 'Continue'

    If ($BuildConfiguration -eq 'release') {
        $BuildConfiguration = 'release'
        $BuildConfigOption = '--release'
    } else {
        $BuildConfiguration = 'debug'
        $BuildConfigOption = ''
    }

    $cargo = Get-CargoCommand -Arm:$Arm
    $ManifestPath = Get-Manifest

    $versionInfoFilePath = Join-Path $env:BUILD_REPOSITORY_LOCALPATH 'versionInfo.json'
    $env:VERSION = Get-Content $versionInfoFilePath | ConvertFrom-JSON | % version
    $env:NO_VALGRIND = 'true'

    $originalRustflags = $env:RUSTFLAGS
    if(-NOT $Arm)
    {
        # only set RUSTFLAGS once for AMD64 and skip setting it in ARM so we don't do it twice
        $env:RUSTFLAGS += ' -C target-feature=+crt-static'
    }
    Write-Host $env:RUSTFLAGS
    Write-Host $env:Path
    Write-Host "$cargo clean"
    Invoke-Expression "$cargo clean"
    Write-Host "$cargo build -p iotedge-diagnostics $(if($Arm) {'--target thumbv7a-pc-windows-msvc'}) $BuildConfigOption --manifest-path $ManifestPath"
    Invoke-Expression "$cargo build -p iotedge-diagnostics $(if($Arm) {'--target thumbv7a-pc-windows-msvc'}) $BuildConfigOption --manifest-path $ManifestPath"
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
        ([IO.Path]::Combine($env:BUILD_REPOSITORY_LOCALPATH, 'edgelet', 'target', $(if($Arm){'\thumbv7a-pc-windows-msvc\'}) + $BuildConfiguration, 'iotedge-diagnostics.exe')) `
        ([IO.Path]::Combine($publishFolder, 'docker', 'windows', $targetArchs[$i]))

    if($Arm -and (-NOT [string]::IsNullOrEmpty($oldPath)))
    {
        $env:path = $oldPath
    }
}
