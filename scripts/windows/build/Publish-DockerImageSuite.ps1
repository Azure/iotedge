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
    [Switch]$FunctionsBinding,

    [Switch]$Push,
    [Switch]$Clean
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$All = -not $Agent -and -not $Hub -and -not $SimulatedTemperatureSensor -and -not $FunctionsBinding
$Images = @{
    "agent"                        = @("Microsoft.Azure.Devices.Edge.Agent.Service", $Agent)
    "hub"                          = @("Microsoft.Azure.Devices.Edge.Hub.Service", $Hub)
    "simulated-temperature-sensor" = @("SimulatedTemperatureSensor", $SimulatedTemperatureSensor)
    "functions-binding"            = @("Microsoft.Azure.Devices.Edge.Functions.Binding", $FunctionsBinding)
}

foreach ($Image in $Images.GetEnumerator()) {
    if (-not $Image.Value[1] -and -not $All) {
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
