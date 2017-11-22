#!/bin/bash

###############################################################################
# This script creates a new deployment for the device provided as argument,
# runs boostrap setup python script and start Edge
###############################################################################

Param(
    # Docker image version. Either use this option or set env variable BUILD_BUILDNUMBER
    [String]$ImageVersion,

    # IoTHub hostname
    [Parameter(Mandatory=$true)]
    [String]$IoTHubHostname,

    # Edge device ID
    [Parameter(Mandatory=$true)]
    [String]$DeviceId,

    # Shared access key used to authenticate the device with IoTHub
    [Parameter(Mandatory=$true)]
    [String]$DeviceAccessKey,
    
    # Shared iothubowner access key used by deployment to authenticate with IoTHub
    [Parameter(Mandatory=$true)]
    [String]$AccessKey,

    # Shared iothubowner access key name used by deployment to authenticate with IoTHub
    [Parameter(Mandatory=$true)]
    [String]$AccessKeyName,

    # Edge Runtime DNS hostname (FQDN). Optional (default: OS reported hostname)
    [String]$EdgeHostname,

    # Azure Edge Modules Container Repositories. CSV expressed as address,username,password.
    [Parameter(Mandatory=$true)]
    [String]$DockerRegistriesCsv,

    # Name of the .zip file containing the iotedgectl files
    [Parameter(Mandatory=$true)]
    [ValidateScript({ Test-Path $_ })]
    [String]$EdgeCtl
)

if (-not $ImageVersion)
{
    $ImageVersion = $Env:BUILD_BUILDNUMBER
    if (-not $ImageVersion)
    {
        throw "Docker image version not found."
    }
}

###############################################################################
# Main Script Execution
###############################################################################
$agent_image_name = "edgebuilds.azurecr.io/azureiotedge/edge-agent-windows-amd64:$ImageVersion"
$device_connection = "HostName=$IoTHubHostname;DeviceId=$DeviceId;SharedAccessKey=$DeviceAccessKey"
$iothub_connection = "HostName=$IoTHubHostname;SharedAccessKeyName=$AccessKeyName;SharedAccessKey=$AccessKey"

echo "Bootstrap Edge"

pushd edge-bootstrap

$EdgeCtlPath = "EdgeCtl"
Expand-Archive -Path $EdgeCtl -DestinationPath $EdgeCtlPath

pushd $EdgeCtlPath
pip install -U .
popd

echo 'Clean up'
Remove-Item -Path $EdgeCtl -Force
Remove-Item -Path $EdgeCtlPath -Force -Recurse

popd

if (-not $EdgeHostname)
{
    $EdgeHostname = "--edge-hostname $EDGE_HOSTNAME"
}

iotedgectl --verbose INFO setup `
    --connection-string $device_connection `
    --image $agent_image_name `
    --docker-uri "npipe://./pipe/docker_engine" `
    --docker-registries ($DockerRegistriesCsv -Split ",") `
    $EdgeHostname

if ($LastExitCode)
{
    Throw "Error running setup RES = $LastExitCode"
}

iotedgectl --verbose INFO start

if ($LastExitCode)
{
    Throw "Error running start RES = $LastExitCode"
}