# Copyright (c) Microsoft. All rights reserved.

New-Item -Type Directory -Force '~/.cargo/bin'
$env:PATH += ";$(Resolve-Path '~/.cargo/bin')"

function Test-RustUp
{
    (get-command -Name rustup.exe -ErrorAction SilentlyContinue) -ne $null
}

function GetPrivateRustPath
{
    Join-Path -Path $(Get-IotEdgeFolder) -ChildPath "rust-windows-arm/rust-windows-arm/bin/"
}

function Get-CargoCommand
{
    Param(
        [Switch] $Arm
    )

    if($Arm)
    {
        # we have private rust arm tool chain downloaded and unzipped to <source root>\rust-windows-arm\rust-windows-arm\cargo.exe
        Join-Path -Path $(GetPrivateRustPath) -ChildPath "cargo.exe"
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

    $ErrorActionPreference = 'Continue'

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
    
    $ErrorActionPreference = 'Stop'
}

function InstallWinArmPrivateRustCompiler
{
    $link = "https://iottools.blob.core.windows.net/iotedge-armtools/rust-windows-arm.zip"

    Write-Host "Downloading $link"
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest $link -out "rust-windows-arm.zip" -UseBasicParsing

    Write-Host "Extracting $link"
    Expand-Archive -Path "rust-windows-arm.zip" -DestinationPath "rust-windows-arm"
    $ProgressPreference = 'Stop'
}

function PatchRustForArm
{
    Param(
        [Switch] $OpenSSL
    )

    $vspath = Join-Path -Path ${env:ProgramFiles(x86)} -ChildPath "Microsoft Visual Studio"
    Write-Host $vspath

    AssertVCToolsForArm

    # arm build requires cl.exe from vc tools to expand a c file for openssl-sys, append x64-x64 cl.exe folder to PATH
    if($OpenSSL)
    {
        try{
            Get-Command cl.exe -ErrorAction Stop
        }
        catch{
            $cls = (Get-ChildItem -Path $vspath -Filter cl.exe -Recurse -ErrorAction Continue -Force | Sort-Object -Property DirectoryName -Descending)
            $clPath = ""
            for ($i= 0; $i -lt $cls.length; $i++) {

                $cl = $cls[$i]
                Write-Host $cl.DirectoryName
                if($cl.DirectoryName.ToLower().Contains("hostx64\x64"))
                {
                    $clPath = $cl.DirectoryName
                    break
                }
            }
            $env:PATH = $clPath + ";" + $env:PATH
            Write-Host $env:PATH
        }

        # test cl.exe command again to make sure we really have it in PATH
        Write-Host $(Get-Command cl.exe).Path
    }

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

[patch."https://github.com/Azure/mio-uds-windows.git"]
mio-uds-windows = { git = "https://github.com/philipktlin/mio-uds-windows.git", branch = "arm" }

"@

    Write-Host "Append cargo.toml with $ForkedCrates"
    Add-Content -Path cargo.toml -Value $ForkedCrates

    $cargo = Get-CargoCommand -Arm

    $ErrorActionPreference = 'Continue'

    Write-Host "$cargo update -p winapi:0.3.5 --precise 0.3.5"
    Invoke-Expression "$cargo update -p winapi:0.3.5 --precise 0.3.5"
    Write-Host "$cargo update -p mio-uds-windows"
    Invoke-Expression "$cargo update -p mio-uds-windows"

    $ErrorActionPreference = 'Stop'
}

function ReplacePrivateRustInPath
{
    Write-Host "Remove cargo path in user profile from PATH, and add the private arm version to the PATH"
    $oldPath = $env:Path
    $paths = $env:Path.Split(";")
    $newPaths = @()
    for($i = 0; $i -lt $paths.length; $i++)
    {
        if(-NOT $paths[$i].Contains(".cargo"))
        {
            # only append path if it does not contain .cargo
            $newPaths += $paths[$i]
        }
        else
        {
            Write-Host "$($paths[$i]) is being removed from PATH"
        }
    }
    $newPaths += $(GetPrivateRustPath)

    $env:path = ""
    for($i = 0; $i -lt $newPaths.length; $i++)
    {
        $env:path += $newPaths[$i] + ";"
    }

    return $oldPath
}

function AssertVCToolsForArm
{
    # it's possible VC tools for arm is not installed, which is required by rust arm compiler
    $vsinstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
    $vstoolsinstallpath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2017\BuildTools"
    $vcarmtools = "Microsoft.VisualStudio.Component.VC.Tools.ARM"

    Write-Host "$vsinstaller --installpath $vstoolsinstallpath modify --add $vcarmtools --quiet --norestart"
    & $vsinstaller --installpath $vstoolsinstallpath modify --add $vcarmtools --quiet --norestart

    $vsinstallerprocessid = (Get-Process vs_installer).id
    Wait-Process -Id $vsinstallerprocessid -Timeout 600 -ErrorAction Stop
}