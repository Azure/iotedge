<#
 # Create a deployment for the current device in the cloud.
 #>

param (
    [Parameter(Mandatory = $true)]
    [ValidateScript( {Invoke-WebRequest $_ -DisableKeepAlive -UseBasicParsing -Method "Head"})]
    [String]$DeployToolUri,

    [Parameter(Mandatory = $true)]
    [String]$IotHubHostName,

    [Parameter(Mandatory = $true)]
    [String]$AccessKey,

    [Parameter(Mandatory = $true)]
    [String]$AccessKeyName,

    [Parameter(Mandatory = $true)]
    [String]$DeviceId,

    [ValidateNotNullOrEmpty()]
    [String]$Agent = "edgebuilds.azurecr.io/azureiotedge/edge-agent-windows-amd64",

    [ValidateNotNullOrEmpty()]
    [String]$Hub = "edgebuilds.azurecr.io/azureiotedge/edge-hub-windows-amd64",

    [ValidateNotNullOrEmpty()]
    [String]$Version = $Env:BUILD_BUILDID,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $AgentWorkFolder,

    [Switch] $Clean
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

if (-not $Version -or $Version -eq "") {
    throw "Docker image version not found. Please specify -Version when re-running this script."
}

$AgentImage = "${Agent}:$Version"
$HubImage = "${Hub}:$Version"
$ConnectionString = "HostName=$IotHubHostName;SharedAccessKeyName=$AccessKeyName;SharedAccessKey=$AccessKey"

$DotnetPath = Join-Path $AgentWorkFolder "dotnet"
$ConfigurationFilePath = "edgeConfiguration.json"
$DownloadPath = Join-Path $Env:TEMP "deploy.zip"
$ExtractionPath = Join-Path $Env:TEMP "deploy"

<#
 # Download deployment tool
 #>

Write-Host "Downloading deployment tool from $DeployToolUri."
Invoke-WebRequest $DeployToolUri $DownloadPath
Expand-Archive $DownloadPath $ExtractionPath -Force

<#
 # Run deployment rool
 #>

Write-Host "Updating configuration to $DeviceId."
$OldPath = $Env:PATH
$Env:PATH = "$Env:PATH;$DotnetPath"
Push-Location $ExtractionPath
try {
    $Config = Get-Content $ConfigurationFilePath | % { $_ -replace "//.*$", "" } | ConvertFrom-Json
    $Config.moduleContent.'$edgeAgent'.'properties.desired'.systemModules.edgeAgent.settings.image = $AgentImage
    $Config.moduleContent.'$edgeAgent'.'properties.desired'.systemModules.edgeHub.settings.image = $HubImage
    $Config.moduleContent.'$edgeAgent'.'properties.desired'.modules = $null
    $Config | ConvertTo-Json -Depth 100 | Out-File $ConfigurationFilePath -Force

    ./edge.cmd configSet -d $DeviceId -c $ConfigurationFilePath -l $ConnectionString
    if ($LASTEXITCODE) {
        throw "Deployment tool exited with exit code $LASTEXITCODE."
    }
}
finally {
    $Env:PATH = $OldPath
    Pop-Location

    if ($Clean) {
        Remove-Item $DownloadPath -Force
        Remove-Item $ExtractionPath -Force -Recurse
    }
}
