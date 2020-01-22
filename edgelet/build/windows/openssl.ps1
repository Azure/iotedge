# Copyright (c) Microsoft. All rights reserved.

function Get-OpenSSL
{
    param (
        [switch] $Arm
    )

    $ErrorActionPreference = 'Continue'

    # Override vcpkg directory from the default $env:HOMEDRIVE\vcpkg that VSTS CI machines have.
    # See TODO below for the reason.
    $vcpkgDirectory = "$env:HOMEDRIVE\vcpkg-2"

    if (!((Test-Path -Path $vcpkgDirectory) -and ((Test-Path -Path $vcpkgDirectory\vcpkg.exe))))
    {
        Write-Host "Installing vcpkg from github..."
        git clone https://github.com/Microsoft/vcpkg $vcpkgDirectory
        if ($LastExitCode)
        {
            Throw "Failed to clone vcpkg repo with exit code $LastExitCode"
        }

        # TODO: vcpkg updated openssl from 1.0.2 to 1.1.1, but some of our libiothsm tests fail with it.
        #       Revert to the version of vcpkg before that commit to fix our build for now,
        #       until we fix the tests to work with 1.1.1.
        Push-Location $vcpkgDirectory
        git checkout 'bdae0904c41a0ee2c5204d6449038d3b5d551726~1'
        Pop-Location

        Write-Host "Bootstrapping vcpkg..."
        & "$vcpkgDirectory\bootstrap-vcpkg.bat"
        if ($LastExitCode)
        {
            Throw "Failed to bootstrap vcpkg with exit code $LastExitCode"
        }
        Write-Host "Installing vcpkg..."
        & $vcpkgDirectory\vcpkg.exe integrate install
        if ($LastExitCode)
        {
            Throw "Failed to install vcpkg with exit code $LastExitCode"
        }
    }

    Write-Host "Downloading strawberry perl"
    if (!(Test-Path -Path $vcpkgDirectory\Downloads))
    {
        New-Item -Type Directory "$vcpkgDirectory\Downloads" | Out-Null
    }

    $strawberryPerlUri = "https://edgebuild.blob.core.windows.net/strawberry-perl/strawberry-perl-5.24.1.1-32bit-portable.zip"
    $strawberryPerlPath = "$vcpkgDirectory\Downloads\strawberry-perl-5.24.1.1-32bit-portable.zip"
    Invoke-WebRequest -Uri $strawberryPerlUri -OutFile $strawberryPerlPath

    Write-Host "Installing OpenSSL for $(if ($Arm) { 'arm' } else { 'x64' })..."
    & $vcpkgDirectory\vcpkg.exe install $(if ($Arm) { 'openssl-windows:arm-windows' } else { 'openssl:x64-windows' } )
    if ($LastExitCode)
    {
        Throw "Failed to install openssl vcpkg with exit code $LastExitCode"
    }

    Write-Host "Setting env variables OPENSSL_ROOT_DIR and OPENSSL_DIR..."
    if ((Test-Path env:TF_BUILD) -and ($env:TF_BUILD -eq $true))
    {
        # When executing within TF (VSTS) environment, install the env variable
        # such that all follow up build tasks have visibility of the env variable
        Write-Host "VSTS installation detected"
        Write-Host "##vso[task.setvariable variable=OPENSSL_ROOT_DIR;]$vcpkgDirectory\installed\$(if ($Arm){ 'arm' } else { 'x64' })-windows"
        # Rust's openssl-sys crate needs this environment set.
        Write-Host "##vso[task.setvariable variable=OPENSSL_DIR;]$vcpkgDirectory\installed\$(if ($Arm){ 'arm' } else { 'x64' })-windows"
    }
    else
    {
        # for local installation, set the env variable within the USER scope
        Write-Host "Local installation detected"
        [System.Environment]::SetEnvironmentVariable("OPENSSL_ROOT_DIR", "$vcpkgDirectory\installed\$(if ($Arm) { 'arm' } else { 'x64' })-windows", [System.EnvironmentVariableTarget]::User)
        [System.Environment]::SetEnvironmentVariable("OPENSSL_DIR", "$vcpkgDirectory\installed\$(if ($Arm) { 'arm' } else { 'x64' })-windows", [System.EnvironmentVariableTarget]::User)
    }

    $ErrorActionPreference = 'Stop'
}
