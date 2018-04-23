<#
    Installs python in a Docker image for use in CI
#>

#Requires -RunAsAdministrator

param (
    [Parameter(Mandatory = $true)]
    [String] $Version
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version "Latest"

<#
    Helper functions
#>

function Invoke-Native {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [String] $Command,

        [Switch] $Passthru
    )

    process {
        Write-Verbose "Executing native Windows command '$Command'..."
        $out = cmd /c "($Command) 2>&1" 2>&1 | Out-String
        Write-Verbose $out
        Write-Verbose "Exit code: $LASTEXITCODE"

        if ($LASTEXITCODE) {
            throw $out
        }
        elseif ($Passthru) {
            $out
        }
    }
}

<#
    Parse version
#>

if (-not ($Version -match "^(\d+)(?:\.(\d+))?(?:\.(\d+)(\w*))?$")) {
    throw "$Version is not a valid version string."
}
$Major = $matches[1]
$Minor = $matches[2]
$Patch = $matches[3]

<#
    Download python
#>

$PythonUrl = "https://www.python.org/ftp/python/${Major}.${Minor}.${Patch}/python-$Version-embed-amd64.zip"
$PythonArchive = Join-Path $env:TEMP "py.zip"

Write-Host "Downloading python from $PythonUrl"
Invoke-WebRequest -Uri $PythonUrl -OutFile $PythonArchive

<#
    Install python
#>

$PythonLibArchive = Join-Path $Env:PYTHONHOME "python$Major$Minor.zip"
$PythonLibDirectory = Join-Path $Env:PYTHONHOME "Lib"
$PythonPathOverride = Join-Path $Env:PYTHONHOME "python$Major$Minor._pth"

Write-Host "Installing python $Version"
try {
    Expand-Archive $PythonArchive $Env:PYTHONHOME -Force
    Expand-Archive $PythonLibArchive $PythonLibDirectory -Force
    Remove-Item $PythonPathOverride -Force -ErrorAction "SilentlyContinue"
}
finally {
    Remove-Item @($PythonArchive, $PythonLibArchive) -Force -ErrorAction "SilentlyContinue"
}

<#
    Install pip
#>

$PipUrl = "https://bootstrap.pypa.io/get-pip.py"
$PipInstaller = Join-Path $Env:PYTHONHOME "get-dependency-manager.py"

Write-Host "Installing pip"
Invoke-WebRequest -Uri $PipUrl -OutFile $PipInstaller
try {
    Invoke-Native "python $PipInstaller"
}
finally {
    Remove-Item $PipInstaller -Force -ErrorAction "SilentlyContinue"
}

Write-Host "Done!"
