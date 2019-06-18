# Copyright (c) Microsoft. All rights reserved.

function Get-Rocksdb
{
    $ErrorActionPreference = 'Continue'

    if (!((Test-Path -Path $env:HOMEDRIVE\vcpkg) -and ((Test-Path -Path $env:HOMEDRIVE\vcpkg\vcpkg.exe))))
    {
        Write-Host "Installing vcpkg from github..."
        git clone https://github.com/Microsoft/vcpkg $env:HOMEDRIVE\vcpkg
        if ($LastExitCode)
        {
            Throw "Failed to clone vcpkg repo with exit code $LastExitCode"
        }
        Write-Host "Bootstrapping vcpkg..."
        & "$env:HOMEDRIVE\vcpkg\bootstrap-vcpkg.bat"
        if ($LastExitCode)
        {
            Throw "Failed to bootstrap vcpkg with exit code $LastExitCode"
        }
        Write-Host "Installing vcpkg..."
        & $env:HOMEDRIVE\\vcpkg\\vcpkg.exe integrate install
        if ($LastExitCode)
        {
            Throw "Failed to install vcpkg with exit code $LastExitCode"
        }
    }

    # checkout the specific commit for ports\rocksdb that matches the rocksdbsharp being used
    # bb1bb1c94a72b891883efa6522791620ef3bbc0f maps to 5.17.2
    # https://github.com/microsoft/vcpkg/commit/bb1bb1c94a72b891883efa6522791620ef3bbc0f#diff-87525ccf58925648e3f92fec94d01d70
    
    push-location $env:HOMEDRIVE\vcpkg    
    git checkout bb1bb1c94a72b891883efa6522791620ef3bbc0f ports\rocksdb
    pop-location
    
    & $env:HOMEDRIVE\vcpkg\vcpkg.exe install rocksdb:arm-windows
    if ($LastExitCode)
    {
        Throw "Failed to install openssl vcpkg with exit code $LastExitCode"
    }
    
    $ErrorActionPreference = 'Stop'
    
    return "$env:HOMEDRIVE\vcpkg\installed\arm-windows\bin\rocksdb-shared.dll"
}
