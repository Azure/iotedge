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
    [String] $Filter
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

$TEST_PROJ_PATTERN = "Microsoft.Azure*test.csproj"
$LOGGER_ARG = "trx;LogFileName=result.trx"

$DOTNET_PATH = [IO.Path]::Combine($AgentWorkFolder, "dotnet", "dotnet.exe")

if (-not (Test-Path $DOTNET_PATH -PathType Leaf)) {
    throw "$DOTNET_PATH not found."
}

<#
 # Run tests
 #>

$BaseTestCommand = if ($Filter) {
    "test --no-build --logger `"$LOGGER_ARG`" --filter `"$Filter`"" 
}
else {
    "test --no-build --logger `"$LOGGER_ARG`""
}

Write-Host "Running tests in all test projects with filter '$Filter'."
$Success = $True
foreach ($Project in (Get-ChildItem $BuildRepositoryLocalPath -Include $TEST_PROJ_PATTERN -Recurse)) {
    Write-Host "Running tests for $Project."
	Write-Host "Run command: '" + $DOTNET_PATH + "' " + $BaseTestCommand " -o " + $BuildBinariesDirectory + " " + $Project
    Invoke-Expression "&`"$DOTNET_PATH`" $BaseTestCommand -o $BuildBinariesDirectory $Project"

    $Success = $Success -and $LASTEXITCODE -eq 0
}

if (-not $Success) {
    throw "Failed tests."
}

Write-Host "Done!"