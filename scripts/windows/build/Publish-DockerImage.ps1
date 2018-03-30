<#
 # Builds a Docker image from a published project.
 #>

param (
    [Parameter(Mandatory = $true)]
    [String]$Name,
    
    [Parameter(Mandatory = $true)]
    [String]$Project,

    [ValidateNotNullOrEmpty()]
    [String]$Version = $Env:BUILD_BUILDID,

    [ValidateNotNullOrEmpty()]
    [String]$Architecture = $Env:PROCESSOR_ARCHITECTURE,

    [ValidateNotNullOrEmpty()]
    [String]$Namespace,

    [ValidateNotNullOrEmpty()]
    [String]$Registry,

    [ValidateNotNullOrEmpty()]
    [String]$BaseTag,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String]$BuildBinariesDirectory = $Env:BUILD_BINARIESDIRECTORY,

    [Switch]$Push,
    [Switch]$Clean
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

<#
 # Prepare environment
 #>

Import-Module ([IO.Path]::Combine($PSScriptRoot, "..", "Defaults.psm1")) -Force

if (-not $BuildBinariesDirectory) {
    $Params = @{}
    if ($Env:BUILD_REPOSITORY_LOCALPATH) {
        $Params["BuildRepositoryLocalPath"] = $Env:BUILD_REPOSITORY_LOCALPATH
    }
    $BuildBinariesDirectory = DefaultBuildBinariesDirectory @Params
}

$SupportedArchs = @("amd64")

$Architecture = $Architecture.ToLower()
if ($SupportedArchs -notcontains $Architecture) {
    throw "'$Architecture' is not a supported architecture."
}

$ProjectDirectory = [IO.Path]::Combine($BuildBinariesDirectory, "publish", $Project)
if (-not (Test-Path $ProjectDirectory -PathType "Container" )) {
    throw "'$ProjectDirectory' is not a directory. Publish-Branch.ps1 must be run before building Docker images."
}

$Dockerfile = [IO.Path]::Combine($ProjectDirectory, "docker", "windows", $Architecture, "Dockerfile")
if (-not (Test-Path $ProjectDirectory)) {
    throw "'$Dockerfile' is not the location of a Dockerfile."
}

if (-not $Version -or $Version -eq "") {
    throw "Docker image version not found. Please specify -Version when re-running this script."
}

$Tag = "${Name}:$Version-windows-$Architecture"
if ($Namespace) {
    $Tag = "$Namespace/$Tag"
}
if ($Registry) {
    $Tag = "$Registry/$Tag"
}

<#
 # Build the image
 #>

$BuildOptions = "--no-cache -t $Tag --file $Dockerfile"
if ($BaseTag) {
    $BuildOptions += " --build-arg base_tag=$BaseTag"
}
$BuildCommand = "docker build $BuildOptions $ProjectDirectory"
Write-Host "Running docker build."
Invoke-Expression $BuildCommand
if ($LASTEXITCODE) {
    throw "'$BuildCommand' failed with exit code $LASTEXITCODE."
}

<#
 # Push the image
 #>

if ($Push) {
    $PushCommand = "docker push $Tag"
    Write-Host "Pushing docker image."
    Invoke-Expression $PushCommand
    if ($LASTEXITCODE) {
        throw "'$PushCommand' failed with exit code $LASTEXITCODE"
    }
}

<#
 # Clean the local store
 #>

if ($Clean) {
    $CleanCommand = "docker rmi $Tag"
    Write-Host "Cleaning local store."
    Invoke-Expression $CleanCommand
    if ($LASTEXITCODE) {
        throw "'$CleanCommand' failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Done!"
