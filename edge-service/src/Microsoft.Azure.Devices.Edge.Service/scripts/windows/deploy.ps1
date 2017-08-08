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

     # Edge Routes
    [Parameter(Mandatory=$false)]
    [String]$Routes,

    # Do not detach after the service starts
    [Switch]$Foreground
)

$docker_routes=""
if ($Routes)
{
    $array = $Routes.Split(",")
    for ($i=0; $i -lt $array.length; $i++)
    {
        $docker_routes += (" -e routes__{0}=`"{1}`"" -f $i, $array[$i])
    }
    Write-Host "Docker routes $docker_routes"
}

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

$Password | docker login $Registry -u $Username --password-stdin
if ($LastExitCode)
{
    Throw "Docker Login Failed With Exit Code $LastExitCode"
}

docker pull $tag
if ($LastExitCode)
{
    Throw "Docker Pull Failed With Exit Code $LastExitCode"
}

& cmd /c "docker stop $image_name 2>&1"

& cmd /c "docker rm $image_name 2>&1"

$run_command = "docker run "
if (-not $Foreground)
{
    $run_command += "-d "
}
$run_command += "--name $image_name -p 8883:8883 -p 443:443 " + 
    "-e DockerUri=http://${DockerAddress}:2375 " + 
    "-e MMAConnectionString='$mma_connection' -e IotHubHostName=$IoTHubHostname " + 
    "-e EdgeDeviceId=$DeviceId" +
    "$docker_routes $tag"

Invoke-Expression $run_command
if ($LastExitCode)
{
    Throw "Docker Run Failed With Exit Code $LastExitCode"
}
