# Copyright (c) Microsoft. All rights reserved.

function Get-OpenSSL
{
    Write-Host "Installing OpenSSL for arm..."
    & $env:sourceroot\vcpkg\bootstrap-vcpkg.bat
    if ($LastExitCode)
    {
        Throw "Failed to bootstrap vcpkg with exit code $LastExitCode"
    }
    & $env:sourceroot\vcpkg\vcpkg.exe integrate install
    if ($LastExitCode)
    {
        Throw "Failed to integrate install for vcpkg $LastExitCode"
    }
    & $env:sourceroot\vcpkg\vcpkg.exe install openssl-windows:arm-windows
    if ($LastExitCode)
    {
        Throw "Failed to install openssl vcpkg with exit code $LastExitCode"
    }

    Write-Host "Setting env variable OPENSSL_ROOT_DIR..."
    if ((Test-Path env:TF_BUILD) -and ($env:TF_BUILD -eq $true))
    {
        # When executing within TF (VSTS) environment, install the env variable
        # such that all follow up build tasks have visibility of the env variable
        Write-Host "VSTS installation detected"
        Write-Host "##vso[task.setvariable variable=OPENSSL_ROOT_DIR;]$env:sourceroot\vcpkg\installed\arm-windows"
        # Rust's openssl-sys crate needs this environment set.
        Write-Host "##vso[task.setvariable variable=OPENSSL_DIR;]$env:sourceroot\vcpkg\installed\arm-windows"
    }
    else
    {
        # for local installation, set the env variable within the USER scope
        Write-Host "Local installation detected"
        [System.Environment]::SetEnvironmentVariable("OPENSSL_ROOT_DIR", "$env:sourceroot\vcpkg\installed\arm-windows", [System.EnvironmentVariableTarget]::User)
        [System.Environment]::SetEnvironmentVariable("OPENSSL_DIR", "$env:sourceroot\vcpkg\installed\arm-windows", [System.EnvironmentVariableTarget]::User)
    }
}
