<#
 # Deploy IoT Edge on the current device
 #>

param (
    [Parameter(Mandatory=$True)]
    [String]$DockerRegistriesCsv,

    [ValidateNotNullOrEmpty()]
    [String]$Agent = "edgebuilds.azurecr.io/azureiotedge/edge-agent-windows-amd64",
    
    [ValidateNotNullOrEmpty()]
    [String]$Version = $Env:BUILD_BUILDID,

    [Parameter(Mandatory=$True)]
    [String]$IotHubHostName,

    [Parameter(Mandatory=$True)]
    [String]$DeviceId,

    [Parameter(Mandatory=$True)]
    [String]$DeviceAccessKey,
    
    [ValidateNotNullOrEmpty()]
    [String]$EdgeHostName
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

if (-not $Version -or $Version -eq "") {
    throw "Docker image version not found. Please specify -Version when re-running this script."
}

$Image = "${Agent}:$Version"
$ConnectionString = "HostName=$IoTHubHostname;DeviceId=$DeviceId;SharedAccessKey=$DeviceAccessKey"

$OldPath = $Env:PATH
$Env:PATH += ";C:\Data\ProgramData\pyiotedge;C:\Data\ProgramData\pyiotedge\scripts;c:\python27\scripts"

try {
    if ($EdgeHostname) {
        iotedgectl --verbose INFO setup `
            --connection-string $ConnectionString `
            --image $Image `
            --docker-uri "npipe://./pipe/docker_engine" `
            --docker-registries ($DockerRegistriesCsv -Split ",") `
            --auto-cert-gen-force-no-passwords `
            --edge-hostname $EdgeHostName
    }
    else {
        iotedgectl --verbose INFO setup `
            --connection-string $ConnectionString `
            --image $Image `
            --docker-uri "npipe://./pipe/docker_engine" `
            --auto-cert-gen-force-no-passwords `
            --docker-registries ($DockerRegistriesCsv -Split ",") 
    }

    if ($LASTEXITCODE)
    {
        throw "Setup failed with exit code $LASTEXITCODE."
    }

    iotedgectl --verbose INFO start

    if ($LASTEXITCODE)
    {
        throw "Start failed with exit code $LASTEXITCODE."
    }
} finally {
    $Env:PATH = $OldPath
}
