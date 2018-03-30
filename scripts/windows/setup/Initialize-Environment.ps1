<#
 # Sets environment variables used by build scripts
 #>

param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $AgentWorkFolder,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $BuildRepositoryLocalPath,
    
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $BuildBinariesDirectory,

    [ValidateSet("Debug", "Release")]
    [String] $Configuration,

    [ValidateNotNull()]
    [String] $BuildId,

    [ValidateNotNull()]
    [String] $BuildSourceVersion,

    [Switch] $Force
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

Import-Module ([IO.Path]::Combine($PSScriptRoot, "..", "Defaults.psm1")) -Force

if ($AgentWorkFolder) {
    $Env:AGENT_WORKFOLDER = $AgentWorkFolder
}
elseif (-not $Env:AGENT_WORKFOLDER -or $Force) {
    $Env:AGENT_WORKFOLDER = DefaultAgentWorkFolder
}

if ($BuildRepositoryLocalPath) {
    $Env:BUILD_REPOSITORY_LOCALPATH = $BuildRepositoryLocalPath
}
elseif (-not $Env:BUILD_REPOSITORY_LOCALPATH -or $Force) {
    $Env:BUILD_REPOSITORY_LOCALPATH = DefaultBuildRepositoryLocalPath
}

if ($BuildBinariesDirectory) {
    $Env:BUILD_BINARIESDIRECTORY = $BuildBinariesDirectory
}
elseif (-not $Env:BUILD_BINARIESDIRECTORY -or $Force) {
    $Env:BUILD_BINARIESDIRECTORY = DefaultBuildBinariesDirectory $Env:BUILD_REPOSITORY_LOCALPATH
}

if ($Configuration) {
    $Env:CONFIGURATION = $Configuration
}
elseif (-not $Env:CONFIGURATION -or $Force) {
    $Env:CONFIGURATION = DefaultConfiguration
}

if ($BuildId) {
    $Env:BUILD_BUILDID = $BuildId
}
elseif (-not $Env:BUILD_BUILDID -or $Force) {
    $Env:BUILD_BUILDID = DefaultBuildId
}

if ($BuildSourceVersion) {
    $Env:BUILD_SOURCEVERSION = $BuildSourceVersion
}
elseif (-not $Env:BUILD_SOURCEVERSION -or $Force) {
    $Env:BUILD_SOURCEVERSION = DefaultBuildSourceVersion
}
