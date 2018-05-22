# Copyright (c) Microsoft. All rights reserved.

function Get-OpenSSL
{
    Write-Host "Installing vcpkg from github"
    git clone https://github.com/Microsoft/vcpkg C:\vcpkg
    if ($LastExitCode)
    {
        Throw "Failed to clone vcpkg repo with exit code $LastExitCode"
    }
    C:\vcpkg\bootstrap-vcpkg.bat
    if ($LastExitCode)
    {
        Throw "Failed to bootstrap vcpkg with exit code $LastExitCode"
    }
    C:\vcpkg\vcpkg integrate install
    if ($LastExitCode)
    {
        Throw "Failed to install vcpkg with exit code $LastExitCode"
    }
    C:\vcpkg\vcpkg install openssl:x64-windows
    if ($LastExitCode)
    {
        Throw "Failed to install openssl vcpkg with exit code $LastExitCode"
    }
    $env:OPENSSL_ROOT_DIR = "C:\\vcpkg\\packages\\openssl_x64-windows"
}
