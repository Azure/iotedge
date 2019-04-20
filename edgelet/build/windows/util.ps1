# Copyright (c) Microsoft. All rights reserved.

New-Item -Type Directory -Force '~/.cargo/bin'
$env:PATH += ";$(Resolve-Path '~/.cargo/bin')"

function Test-RustUp
{
    (get-command -Name rustup.exe -ErrorAction SilentlyContinue) -ne $null
}

function Get-CargoCommand
{
    Param(
        [Switch] $Arm
    )

    if($Arm)
    {
        # we have private rust arm tool chain downloaded and unzipped to <source root>\rust-windows-arm\rust-windows-arm\cargo.exe
        Join-Path -Path $(Get-IotEdgeFolder) -ChildPath "rust-windows-arm/rust-windows-arm/bin/cargo.exe"
    }
    elseif (Test-RustUp)
    {
        'cargo +stable-x86_64-pc-windows-msvc '
    }
    else
    {
        "$env:USERPROFILE/.cargo/bin/cargo.exe +stable-x86_64-pc-windows-msvc "
    }
}

function Get-Manifest
{
    $ProjectRoot = Join-Path -Path $PSScriptRoot -ChildPath "../../.."
    Join-Path -Path $ProjectRoot -ChildPath "edgelet/Cargo.toml"
}

function Get-EdgeletFolder
{
    $ProjectRoot = Join-Path -Path $PSScriptRoot -ChildPath "../../.."
    Join-Path -Path $ProjectRoot -ChildPath "edgelet"
}

function Get-IotEdgeFolder
{
    # iotedge is parent folder of edgelet
    Join-Path -Path $(Get-EdgeletFolder) -ChildPath ".."
}

function Assert-Rust
{
Param(
    [Switch] $Arm
)
    if($Arm)
    {
        if(-NOT (Test-Path "rust-windows-arm"))
        {
            # if the folder rust-windows-arm exists, we assume the private rust compiler for arm is installed
            InstallWinArmPrivateRustCompiler
        }
    }
    elseif (-not (Test-RustUp))
    {
        Write-Host "Installing rustup and stable-x86_64-pc-windows-msvc Rust."
        Invoke-RestMethod -usebasicparsing 'https://static.rust-lang.org/rustup/dist/i686-pc-windows-gnu/rustup-init.exe' -outfile 'rustup-init.exe'
        if ($LastExitCode)
        {
            Throw "Failed to download rustup with exit code $LastExitCode"
        }

        Write-Host "Running rustup-init.exe"
        ./rustup-init.exe -y --default-toolchain stable-x86_64-pc-windows-msvc
        if ($LastExitCode)
        {
            Throw "Failed to install rust with exit code $LastExitCode"
        }
    }
    else
    {
        Write-Host "Running rustup.exe"
        rustup install stable-x86_64-pc-windows-msvc
        if ($LastExitCode)
        {
            Throw "Failed to install rust with exit code $LastExitCode"
        }
    }
}

function InstallWinArmPrivateRustCompiler
{
    $link = "https://iottools.blob.core.windows.net/iotedge-armtools/rust-windows-arm.zip"

    Write-Host "Downloading $link"
    $ProgressPreference = 'Continue'
    Invoke-WebRequest $link -out "rust-windows-arm.zip" -UseBasicParsing

    Write-Host "Extracting $link"
    Expand-Archive -Path "rust-windows-arm.zip" -DestinationPath "rust-windows-arm"
    $ProgressPreference = 'Stop'
}