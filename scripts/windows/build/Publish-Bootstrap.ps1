<#
 # Builds a source distribution for the Python bootstrap script
 #>

param (
    [ValidateNotNullOrEmpty()]
    [String] $EggInfoOptions,

    [ValidateNotNullOrEmpty()]
    [String] $SDistOptions,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $BuildRepositoryLocalPath = $Env:BUILD_REPOSITORY_LOCALPATH,

    [ValidateNotNullOrEmpty()]
    [String] $BuildBinariesDirectory = $Env:BUILD_BINARIESDIRECTORY
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

<#
 # Prepare environment
 #>

Import-Module ([IO.Path]::Combine($PSScriptRoot, "..", "Defaults.psm1")) -Force

if (-not $BuildRepositoryLocalPath) {
    $BuildRepositoryLocalPath = DefaultBuildRepositoryLocalPath
}

if (-not $BuildBinariesDirectory) {
    $BuildBinariesDirectory = DefaultBuildBinariesDirectory $BuildRepositoryLocalPath
}

Push-Location $BuildRepositoryLocalPath\edge-bootstrap\python

<#
 # Create source distribution
 #>

Write-Host "Creating source distribution for Python bootstrap script."
$Python = Join-Path $Env:SystemDrive "python27\python.exe"
$Command = if ($EggInfoOptions) {
    "$Python setup.py egg_info $EggInfoOptions sdist --dist-dir $BuildBinariesDirectory $SDistOptions 2>&1"
}
else {
    "$Python setup.py sdist --dist-dir $BuildBinariesDirectory $SDistOptions 2>&1"
}

try {
    cmd /c $Command
    if ($LASTEXITCODE) {
        throw "'$Command' failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

