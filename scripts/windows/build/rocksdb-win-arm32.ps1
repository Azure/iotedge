# Copyright (c) Microsoft. All rights reserved.

function Get-Rocksdb
{
    $ErrorActionPreference = 'Continue'

    if (!(Test-Path -Path $env:HOMEDRIVE\vcpkg1))
    {
        Write-Host "git clone vcpkg"
        git clone https://github.com/Microsoft/vcpkg $env:HOMEDRIVE\vcpkg1
        if ($LastExitCode)
        {
            Throw "Failed to clone vcpkg repo with exit code $LastExitCode"
        }
    }

    # checkout the specific commit for ports\rocksdb that matches the rocksdbsharp being used
    # bb1bb1c94a72b891883efa6522791620ef3bbc0f maps to 5.17.2
    # https://github.com/microsoft/vcpkg/commit/bb1bb1c94a72b891883efa6522791620ef3bbc0f#diff-87525ccf58925648e3f92fec94d01d70
    
    push-location $env:HOMEDRIVE\vcpkg1
    git pull
    git checkout bb1bb1c94a72b891883efa6522791620ef3bbc0f ports\rocksdb
    pop-location    
     
    Write-Host "always rerun bootstrap-vcpkg.bat"
    & "$env:HOMEDRIVE\vcpkg1\bootstrap-vcpkg.bat"
    if ($LastExitCode)
    {
        Throw "Failed to bootstrap vcpkg with exit code $LastExitCode"
    }
        
    Write-Host "vcpkg.exe integrate install"
    & $env:HOMEDRIVE\\vcpkg1\\vcpkg.exe integrate install
    if ($LastExitCode)
    {
        Throw "Failed to install vcpkg with exit code $LastExitCode"
    }
    
    push-location $env:HOMEDRIVE\vcpkg1
    Write-Host "vcpkg.exe install rocksdb:arm-windows"
    vcpkg.exe install rocksdb:arm-windows
    pop-location
    if ($LastExitCode)
    {
        Throw "Failed to install rocksdb vcpkg with exit code $LastExitCode"
    }
    
    $ErrorActionPreference = 'Stop'
    
    return "$env:HOMEDRIVE\vcpkg1\installed\arm-windows\bin\rocksdb-shared.dll"
}
