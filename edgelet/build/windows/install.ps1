# Copyright (c) Microsoft. All rights reserved.

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

Assert-Rust

# Bring in openssl install function
$openssl = Join-Path -Path $PSScriptRoot -ChildPath "openssl.ps1"
. $openssl

Get-OpenSSL
