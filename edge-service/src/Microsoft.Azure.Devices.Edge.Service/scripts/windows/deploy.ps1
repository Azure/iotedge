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

    # Docker Engine host address
    [Parameter(Mandatory=$true)]
    [String]$DockerAddress,

    # Do not detach after the service starts
    [Switch]$Foreground
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
$mma_connection = "HostName=$IoTHubHostname;DeviceId=$DeviceId;SharedAccessKey=$AccessKey"
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

$run_command = "docker run "
if (-not $Foreground)
{
    $run_command += "-d "
}
$run_command += "--name $image_name -p 8883:8883 -p 443:443 " + 
    "-e DockerUri=http://${DockerAddress}:2375 " + 
    "-e MMAConnectionString='$mma_connection' -e IotHubHostName=$IoTHubHostname " + 
    "-e EdgeDeviceId=$DeviceId $tag"

Invoke-Expression $run_command
if ($LastExitCode)
{
    Throw "Docker Run Failed With Exit Code $LastExitCode"
}
