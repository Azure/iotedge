# Copyright (c) Microsoft. All rights reserved.


# The Windows ARM32 build requires all uses of winapi 0.2 to be removed,
# since that crate does not build for the Windows ARM32 target.
# Some mio / tokio crates depend on winapi 0.2, so the build
# `[patch]`es them to versions that depend on winapi 0.3 instead.
# See `function PatchRustForArm` for details.
#
# This script runs just the patch step of the actual Windows ARM32 build,
# and ensures that winapi 0.2 is removed from the lockfile.
#
# For checkin jobs this helps prevent someone from adding a new dependency
# that pulls in winapi 0.2.
#
# A proper build would also catch this, and other errors, but takes more time
# than we'd like for a checkin job.


# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath 'util.ps1'
. $util

# Note: This is destructive since it modifies the manifest, lockfile and environment.
#       Ensure that no new x86_64-pc-windows-msvc builds happen after this script runs.
Assert-Rust -Arm
PatchRustForArm

$cargoLockContent = [System.IO.File]::ReadAllText((Join-Path (Get-EdgeletFolder) -ChildPath 'Cargo.lock'))

if ($cargoLockContent.Contains("[[package]]`nname = `"winapi`"`nversion = `"0.2")) {
	throw (
		'Cargo.lock still references winapi 0.2 after patching. ' +
		'Ensure that there are no new dependencies that depend on winapi 0.2'
	)
}
