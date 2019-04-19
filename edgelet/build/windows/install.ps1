# Copyright (c) Microsoft. All rights reserved.

param(
    [switch]$Arm
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

# Currently window arm rust compiler is using local version on the build machine
# So we skip asserting rust for arm
if(-Not $Arm)
{
    Assert-Rust
}

# Bring in openssl install function
$openssl = Join-Path -Path $PSScriptRoot -ChildPath "openssl.ps1"
. $openssl

Get-OpenSSL -Arm:$Arm
