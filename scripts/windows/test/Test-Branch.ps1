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

$testProjectRunSerially = @( "Microsoft.Azure.Devices.Edge.Agent.Docker.Test.dll" )
$testProjectDllsRunSerially = @()
$testProjectsDlls = ""
foreach ($testDll in (Get-ChildItem $BuildBinariesDirectory -Include $SUFFIX -Recurse)) {
    Write-Host "Found test project:$testDll"
    
	if (($testProjectRunSerially | ?{ $testDll.FullName.EndsWith("\$_") }) -ne $null)
	{
		Write-Host "Run Serially for $testDll"
		$testProjectDllsRunSerially += $testDll.FullName
	}
	else
	{
		$testProjectsDlls += " $testDll"
	}
}

$testCommandPrefix = "$DOTNET_PATH vstest /Logger:`"$LOGGER_ARG`" /TestAdapterPath:`"$BuildBinariesDirectory`" /Parallel /InIsolation"

if ($Filter) {
    $testCommandPrefix += " /TestCaseFilter:`"$Filter`"" 
}

foreach($testDll in $testProjectDllsRunSerially)
{
	$testCommand = "$testCommandPrefix $testDll"
	Write-Host "Run test command serially: $testCommand"
	Invoke-Expression "$testCommand"
}

$testCommand = $testCommandPrefix + $testProjectsDlls
Write-Host "Run test command: $testCommand"
Invoke-Expression "$testCommand"

Write-Host "Done!"
