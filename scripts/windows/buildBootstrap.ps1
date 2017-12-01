Param(
    # Parameters to pass to egg-info (e.g. \"--tag-build=dev --tag-date\")
    [String]$EggInfo,

    # Output directory for the package
    [String]$OutputDir,

    # Additional options for sdist
    [String]$SDistOptions
)

# This script builds a source distribution for the python bootstrap script
# and places it in $BUILD_BINARIESDIRECTORY.

if (-not $Env:BUILD_REPOSITORY_LOCALPATH)
{
    $Env:BUILD_REPOSITORY_LOCALPATH = Join-Path $PSScriptRoot "..\.."
}

if (-not $Env:BUILD_BINARIESDIRECTORY)
{
    $Env:BUILD_BINARIESDIRECTORY = Join-Path $Env:BUILD_REPOSITORY_LOCALPATH "target"
}

$ROOT_FOLDER = $Env:BUILD_REPOSITORY_LOCALPATH

if (-not (Test-Path $ROOT_FOLDER))
{
    Throw "Folder $ROOT_FOLDER does not exist"
}

if (-not $OutputDir)
{
    $OutputDir = $Env:BUILD_BINARIESDIRECTORY
}

if (-not (Test-Path $OutputDir))
{
    mkdir $OutputDir
}

Push-Location $ROOT_FOLDER\edge-bootstrap\python

$Python = Join-Path $Env:SystemDrive "python27\python.exe"

echo "Creating source distribution for python bootstrap script"
if ($EggInfo)
{
    cmd /c "$Python setup.py egg_info $EggInfo sdist --dist-dir $OutputDir $SDistOptions 2>&1"
}
else 
{
    cmd /c "$Python setup.py sdist --dist-dir $OutputDir $SDistOptions 2>&1"
}

if ($LastExitCode)
{
    Throw "Setup.py egg_info/sdist Failed With Exit Code $LastExitCode"
}

<#echo "Creating wheel"
if ($EggInfo)
{
    Invoke-Expression "cmd /c `"$Python setup.py egg_info $EggInfo bdist_wheel --dist-dir $OutputDir 2>&1`""
}
else 
{
    Invoke-Expression "cmd /c `"$Python setup.py bdist_wheel --dist-dir $OutputDir 2>&1`""
}
#>
