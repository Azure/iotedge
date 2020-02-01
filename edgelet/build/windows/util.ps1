# Copyright (c) Microsoft. All rights reserved.

New-Item -Type Directory -Force '~/.cargo/bin'
$env:PATH += ";$(Resolve-Path '~/.cargo/bin')"

function Test-RustUp
{
    (get-command -Name rustup.exe -ErrorAction SilentlyContinue) -ne $null
}

function GetPrivateRustPath
{
    $toolchain = Get-Content -Encoding UTF8 (Join-Path -Path (Get-EdgeletFolder) -ChildPath 'rust-toolchain')
    $targetPath = Join-Path -Path (Get-IotEdgeFolder) -ChildPath "rust-windows-arm-$toolchain"
    Join-Path -Path $targetPath -ChildPath 'rust-windows-arm/bin/'
}

function Get-CargoCommand
{
    param (
        [switch] $Arm
    )

    if ($Arm) {
        # we have private rust arm tool chain downloaded and unzipped to <source root>\rust-windows-arm\rust-windows-arm\cargo.exe
        Join-Path -Path (GetPrivateRustPath) -ChildPath 'cargo.exe'
    }
    else {
        'cargo'
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
    param (
        [switch] $Arm
    )

    $ErrorActionPreference = 'Continue'

    $toolchain = Get-Content -Encoding UTF8 (Join-Path -Path (Get-EdgeletFolder) -ChildPath 'rust-toolchain')

    if ($Arm) {
        if (-not (Test-Path (GetPrivateRustPath))) {
            InstallWinArmPrivateRustCompiler
        }
    }
    else {
        if (-not (Test-RustUp)) {
            Write-Host "Installing rustup"
            Invoke-RestMethod -usebasicparsing 'https://static.rust-lang.org/rustup/dist/i686-pc-windows-gnu/rustup-init.exe' -outfile 'rustup-init.exe'
            if ($LastExitCode)
            {
                Throw "Failed to download rustup with exit code $LastExitCode"
            }

            Write-Host "Running rustup-init.exe"
            ./rustup-init.exe -y
            if ($LastExitCode)
            {
                Throw "Failed to install rust with exit code $LastExitCode"
            }
        }

        Write-Host "Installing / updating $toolchain toolchain"
        rustup update $toolchain
        if ($LastExitCode)
        {
            Throw "Failed to install rust with exit code $LastExitCode"
        }
    }

    $ErrorActionPreference = 'Stop'
}

function InstallWinArmPrivateRustCompiler {
    $ProgressPreference = 'SilentlyContinue'

    $toolchain = Get-Content -Encoding UTF8 (Join-Path -Path (Get-EdgeletFolder) -ChildPath 'rust-toolchain')

    $downloadPath = (Join-Path -Path (Get-IotEdgeFolder) -ChildPath "rust-windows-arm-$toolchain.zip")
    if (-not (Test-Path $downloadPath)) {
        $link = "https://edgebuild.blob.core.windows.net/iotedge-win-arm32v7-tools/rust-windows-arm-$toolchain.zip"

        Write-Host "Downloading $link to $downloadPath"
        Invoke-WebRequest $link -OutFile $downloadPath -UseBasicParsing
    }

    $targetPath = Join-Path -Path (Get-IotEdgeFolder) -ChildPath "rust-windows-arm-$toolchain"
    Write-Host "Extracting $downloadPath to $targetPath"
    if (Test-Path $targetPath) {
        Remove-Item -Recurse -Force $targetPath
    }
    Expand-Archive -Path $downloadPath -DestinationPath $targetPath

    $ProgressPreference = 'Stop'
}

# arm build has to use a few private forks of dependencies instead of the public ones, in order to to this, we have to 
# 1. append a [patch] section in cargo.toml to use crate forks
# 2. run cargo update commands to force update cargo.lock to use the forked crates
# 3 (optional). when building openssl-sys, cl.exe is called to expand a c file, we need to put the hostx64\x64 cl.exe folder to PATH so cl.exe can be found
#   this is optional because when building iotedge-diagnostics project, openssl is not required
function PatchRustForArm {
    param (
        [switch] $OpenSSL
    )

    $vsPath = Join-Path -Path ${env:ProgramFiles(x86)} -ChildPath 'Microsoft Visual Studio'
    Write-Host $vsPath

    # arm build requires cl.exe from vc tools to expand a c file for openssl-sys, append x64-x64 cl.exe folder to PATH
    if ($OpenSSL) {
        try {
            Get-Command cl.exe -ErrorAction Stop
        }
        catch {
            $cls = Get-ChildItem -Path $vsPath -Filter cl.exe -Recurse -ErrorAction Continue -Force | Sort-Object -Property DirectoryName -Descending
            $clPath = ''
            for ($i = 0; $i -lt $cls.length; $i++) {
                $cl = $cls[$i]
                Write-Host $cl.DirectoryName
                if ($cl.DirectoryName.ToLower().Contains('hostx64\x64')) {
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

    $ForkedCrates = @"

[patch.crates-io]
iovec = { git = "https://github.com/philipktlin/iovec", branch = "arm" }
mio = { git = "https://github.com/philipktlin/mio", branch = "arm" }
miow = { git = "https://github.com/philipktlin/miow", branch = "arm" }
winapi = { git = "https://github.com/philipktlin/winapi-rs", branch = "arm/v0.3.5" }

[patch."https://github.com/Azure/mio-uds-windows.git"]
mio-uds-windows = { git = "https://github.com/philipktlin/mio-uds-windows.git", branch = "arm" }

"@

    $ManifestPath = Get-Manifest
    Write-Host "Add-Content -Path $ManifestPath -Value $ForkedCrates"
    Add-Content -Path $ManifestPath -Value $ForkedCrates

    $cargo = Get-CargoCommand -Arm

    $ErrorActionPreference = 'Continue'

    Write-Host "$cargo update -p winapi:0.3.5 --precise 0.3.5 --manifest-path $ManifestPath"
    Invoke-Expression "$cargo update -p winapi:0.3.5 --precise 0.3.5 --manifest-path $ManifestPath"
    Write-Host "$cargo update -p mio-uds-windows --manifest-path $ManifestPath"
    Invoke-Expression "$cargo update -p mio-uds-windows --manifest-path $ManifestPath"

    $ErrorActionPreference = 'Stop'
}

function ReplacePrivateRustInPath {
    Write-Host 'Remove cargo path in user profile from PATH, and add the private arm version to the PATH'

    $oldPath = $env:PATH

    [string[]] $newPaths = $env:PATH -split ';' |
        ?{
            $removePath = $_.Contains('.cargo')
            if ($removePath) {
                Write-Host "$_ is being removed from PATH"
            }
            -not $removePath
        }
    $newPaths += GetPrivateRustPath
    $env:PATH = $newPaths -join ';'

    $oldPath
}
