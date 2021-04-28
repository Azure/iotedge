# Copyright (c) Microsoft. All rights reserved.

param(
    [switch] $Arm
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

Assert-Rust -Arm:$Arm

# # Bring in openssl install function
# $openssl = Join-Path -Path $PSScriptRoot -ChildPath "openssl.ps1"
# . $openssl

# Get-OpenSSL -Arm:$Arm
