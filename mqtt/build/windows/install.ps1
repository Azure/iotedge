# Copyright (c) Microsoft. All rights reserved.

param(
    [switch] $Arm
)

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

Assert-Rust -Arm:$Arm
