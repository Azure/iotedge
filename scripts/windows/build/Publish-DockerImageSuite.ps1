<#
 # Builds Docker images from all published projects.
 #>

param (
    [ValidateNotNullOrEmpty()]
    [String]$Version,

    [ValidateNotNullOrEmpty()]
    [String]$Architecture,

    [ValidateNotNullOrEmpty()]
    [String]$Namespace,

    [ValidateNotNullOrEmpty()]
    [String]$Registry,

    [ValidateNotNullOrEmpty()]
    [String]$BaseTag,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String]$BuildBinariesDirectory,

    [Switch]$Agent,
    [Switch]$Hub,
    [Switch]$SimulatedTemperatureSensor,
    [Switch]$TemperatureFilter,
    [Switch]$DirectMethodSender,
    [Switch]$DirectMethodReceiver,
    [Switch]$TemperatureFilterFunction,

    [Switch]$Push,
    [Switch]$Clean
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$All = -not $Agent -and -not $Hub -and -not $SimulatedTemperatureSensor -and -not $TemperatureFilter
$Images = @{
    "agent"                        = @("Microsoft.Azure.Devices.Edge.Agent.Service", $Agent)
    "hub"                          = @("Microsoft.Azure.Devices.Edge.Hub.Service", $Hub)
    "simulated-temperature-sensor" = @("SimulatedTemperatureSensor", $SimulatedTemperatureSensor)
    "temperature-filter"           = @("TemperatureFilter", $TemperatureFilter)
    "direct-method-sender"         = @("DirectMethodSender", $DirectMethodSender)
    "direct-method-receiver"       = @("DirectMethodReceiver", $DirectMethodReceiver)	
    "functions-filter"             = @("EdgeHubTriggerCSharp", $TemperatureFilterFunction)
}

foreach ($Image in $Images.GetEnumerator()) {
    if (-not $Image.Value[1] -and -not $All) {
        continue
    }

    if ($Image.Key -eq "functions-filter" && $Architecture -ne "amd64") {
        # skip functions filter temporarily as waiting for azure function docker image
		Write-Host "Skip function filter docker image build since azure function base docker image is not available yet."
        continue
    }
    
    $Name = "azureiotedge-$($Image.Key)"
    $Params = @{
        "Name"    = $Name
        "Project" = $Image.Value[0]
        "Push"    = $Push
        "Clean"   = $Clean
    }
    if ($Version) {
        $Params["Version"] = $Version
    }
    if ($Architecture) {
        $Params["Architecture"] = $Architecture
    }
    if ($Registry) {
        $Params["Registry"] = $Registry
    }
    if ($Namespace) {
        $Params["Namespace"] = $Namespace
    }
    if ($BaseTag) {
        $Params["BaseTag"] = $BaseTag
    }
    if ($BuildBinariesDirectory) {
        $Params["BuildBinariesDirectory"] = $BuildBinariesDirectory
    }

    Write-Host "Building $Name from $($Image.Value[0])."
    &(Join-Path $PSScriptRoot "Publish-DockerImage.ps1") @Params
}
