# Copyright (c) Microsoft. All rights reserved.

function Get-Rocksdb-WinArm32
{
    $ErrorActionPreference = 'Continue'

    $vcpkgroot = "$env:HOMEDRIVE\vcpkg1"

    if (!(Test-Path -Path $vcpkgroot))
    {
        Write-Host "git clone vcpkg"
        git clone https://github.com/Microsoft/vcpkg $vcpkgroot
        if ($LastExitCode)
        {
            Throw "Failed to clone vcpkg repo with exit code $LastExitCode"
        }
    }

    # checkout the specific commit that matches the rocksdbsharp being used
    # 4d224838df2da68def51540e328b8b692ae5607f maps to 5.18.3
    # https://github.com/microsoft/vcpkg/commit/4d224838df2da68def51540e328b8b692ae5607f#diff-87525ccf58925648e3f92fec94d01d70
    
    push-location $vcpkgroot

    git reset --hard | Write-Host
    git pull | Write-Host
    git checkout 4d224838df2da68def51540e328b8b692ae5607f | Write-Host

    Write-Host "bootstrap-vcpkg.bat"
    .\bootstrap-vcpkg.bat | Write-Host
    if ($LastExitCode)
    {
        Throw "Failed to bootstrap vcpkg with exit code $LastExitCode"
    }
        
    Write-Host "vcpkg.exe integrate install"
    .\vcpkg.exe integrate install | Write-Host
    if ($LastExitCode)
    {
        Throw "Failed to install vcpkg with exit code $LastExitCode"
    }
    
    Write-Host "vcpkg.exe install rocksdb:arm-windows"
    .\vcpkg.exe install rocksdb:arm-windows | Write-Host
    if ($LastExitCode)
    {
        Throw "Failed to install rocksdb vcpkg with exit code $LastExitCode"
    }

    $rocksdbdll = "$vcpkgroot\installed\arm-windows\bin\rocksdb-shared.dll"

    if(!(Test-Path -Path $rocksdbdll))
    {
        Throw "rocksdb dll is not found at $rocksdbdll"
    }
    
    $ErrorActionPreference = 'Stop'

    pop-location
    
    return $rocksdbdll
}
