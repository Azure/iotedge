<#
 # Runs all .NET Core test projects in the repo
 #>

param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $AgentWorkFolder = $Env:AGENT_WORKFOLDER,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $BuildRepositoryLocalPath = $Env:BUILD_REPOSITORY_LOCALPATH,
    
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $BuildBinariesDirectory = $Env:BUILD_BINARIESDIRECTORY,

    [ValidateNotNullOrEmpty()]
    [String] $Filter,
    
    [String] $BuildConfig
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

<#
 # Prepare environment
 #>

Import-Module ([IO.Path]::Combine($PSScriptRoot, "..", "Defaults.psm1")) -Force

if (-not $AgentWorkFolder) {
    $AgentWorkFolder = DefaultAgentWorkFolder
}

if (-not $BuildRepositoryLocalPath) {
    $BuildRepositoryLocalPath = DefaultBuildRepositoryLocalPath
}

if (-not $BuildBinariesDirectory) {
    $BuildBinariesDirectory = DefaultBuildBinariesDirectory $BuildRepositoryLocalPath
}

$SUFFIX = "Microsoft.Azure*test.dll"
$LOGGER_ARG = "trx;LogFileName=result.trx"
$DOTNET_PATH = [IO.Path]::Combine($AgentWorkFolder, "dotnet", "dotnet.exe")

if (-not (Test-Path $DOTNET_PATH -PathType Leaf)) {
    throw "$DOTNET_PATH not found."
}

if (-not $BuildConfig) {
    $BuildConfig = "CheckInBuild"
}

<#
 # Run tests
 #>
Write-Host "Running tests in all test projects with filter '$Filter' and $BuildConfig configuration."

$testProjectsDlls = ""
foreach ($testDll in (Get-ChildItem $BuildBinariesDirectory -Include $SUFFIX -Recurse)) {
    #$fileBaseName = [System.IO.Path]::GetFileNameWithoutExtension($Project)
    #$parentDirectory = Split-Path -Path $Project
    #$currentTestProjectDll = " $parentDirectory\bin\$BuildConfig\netcoreapp2.1\$fileBaseName.dll"
    #Write-Host "Found test project:$currentTestProjectDll"
    #$testProjectsDlls += $currentTestProjectDll
    Write-Host "Found test project:$testDll"
    $testProjectsDlls += " $testDll"
}

$testCommand = "$DOTNET_PATH vstest /Logger:`"$LOGGER_ARG`" /TestAdapterPath:`"$BuildBinariesDirectory`" /Parallel /InIsolation"

if ($Filter) {
    $testCommand += " /TestCaseFilter:`"$Filter`"" 
}
$testCommand += "$testProjectsDlls"

Write-Host "Run test command:$testCommand"
Invoke-Expression "$testCommand"
Write-Host "Last exit code=$LASTEXITCODE"

Write-Host "Done!"