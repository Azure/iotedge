###############################################################################
# This script pulls Edge service docker image from the repository, stops the 
# container which runs the Edge service and starts it with the new image
###############################################################################

Param(
    # Docker registry required to pull the Edge service image
    [Parameter(Mandatory=$true)]
    [String]$Registry,

    # Docker registry username
    [Parameter(Mandatory=$true)]
    [String]$Username,

    # Docker registry password
    [Parameter(Mandatory=$true)]
    [String]$Password,

    # Docker image version. Either use this option or set env variable BUILD_BUILDNUMBER
    [ValidateNotNullOrEmpty()]
    [String]$ImageVersion,

    # IoTHub hostname
    [Parameter(Mandatory=$true)]
    [String]$IoTHubHostname,

    # Edge device ID
    [Parameter(Mandatory=$true)]
    [String]$DeviceId,

    # Shared access key used to authenticate the device with IoTHub
    [Parameter(Mandatory=$true)]
    [String]$AccessKey,

    # Edge hostname
    [Parameter(Mandatory=$true)]
    [String]$EdgeHostname,

    # Docker Engine host address
    [Parameter(Mandatory=$true)]
    [String]$DockerAddress
)

if (-not $ImageVersion)
{
    if ($Env:BUILD_BUILDNUMBER)
    {
        $ImageVersion = $Env:BUILD_BUILDNUMBER
    }
    else
    {
        Throw "Docker image version not found."
    }
}

$image_name = "edge-service"
$mma_connection = "HostName=$IoTHubHostname;GatewayHostname=$EdgeHostname;DeviceId=$DeviceId;SharedAccessKey=$AccessKey"
$tag = "edgebuilds.azurecr.io/azedge-edge-service-windows-x64:$ImageVersion"

docker login $Registry -u $Username -p $Password
if ($LastExitCode)
{
    Throw "Docker Login Failed With Exit Code $LastExitCode"
}

docker pull $tag
if ($LastExitCode)
{
    Throw "Docker Pull Failed With Exit Code $LastExitCode"
}

docker stop $image_name

docker rm $image_name

docker run -d --name $image_name -p 8883:8883 -p 443:443 -e IPInterfaceName=Ethernet -e DockerUri=http://${DockerAddress}:2375 -e MMAConnectionString=$mma_connection -e IotHubHostName=$IoTHubHostname -e EdgeDeviceId=$DeviceId $tag
if ($LastExitCode)
{
    Throw "Docker Run Failed With Exit Code $LastExitCode"
}
