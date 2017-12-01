###############################################################################
# This script creates a new deployment for the device provided as argument
###############################################################################

Param(
    [Parameter(Mandatory=$true)]
    [String]$DeployTool,

    # Docker image version. Either use this option or set env variable BUILD_BUILDNUMBER
    [String]$ImageVersion,

    # IoTHub hostname
    [Parameter(Mandatory=$true)]
    [String]$IoTHubHostname,

    [Parameter(Mandatory=$true)]
    [String]$AccessKey,

    [Parameter(Mandatory=$true)]
    [String]$AccessKeyName,

    # Edge device ID
    [Parameter(Mandatory=$true)]
    [String]$DeviceId
)

if (-not $ImageVersion)
{
    $ImageVersion = $Env:BUILD_BUILDNUMBER
    if (-not $ImageVersion)
    {
        throw "Docker image version not found."
    }
}

function update_config_file($cfg_file)
{
    $config = Get-Content $cfg_file | % { $_ -replace "//.*$","" } | ConvertFrom-Json

    $config.moduleContent.'$edgeAgent'.'properties.desired'.systemModules.edgeAgent.settings.image = 
        $script:agent_image_name

    $config.moduleContent.'$edgeAgent'.'properties.desired'.systemModules.edgeHub.settings.image = 
        $script:edgehub_image_name

    $config.moduleContent.'$edgeAgent'.'properties.desired'.modules = $null

    ConvertTo-Json $cfg_file -Depth 100
}

###############################################################################
# Main Script Execution
###############################################################################
$script:agent_image_name = "edgebuilds.azurecr.io/azureiotedge/edge-agent-windows-amd64:$ImageVersion"
$script:edgehub_image_name = "edgebuilds.azurecr.io/azureiotedge/edge-hub-windows-amd64:$ImageVersion"
$iothub_connection = "HostName=$IoTHubHostname;SharedAccessKeyName=$AccessKeyName;SharedAccessKey=$AccessKey"
$deploy_tool_path = ".\deploy"

if (Test-Path $deploy_tool_path)
{
    Remove-Item -Path $deploy_tool_path -Recurse -Force
}
mkdir $deploy_tool_path

echo "Downloading package $DeployTool"
$DeployToolDownload = ".\deploy.zip"
Invoke-WebRequest -Uri $DeployTool -OutFile $DeployToolDownload
Expand-Archive -Path $DeployToolDownload -DestinationPath ".\"

echo "Set configuration to $DeviceId"
pushd $deploy_tool_path

$ConfigurationFilePath = "edgeConfiguration.json"

update_config_file $ConfigurationFilePath

$dotnet = Join-Path -Path $env:AGENT_WORKFOLDER -ChildPath "dotnet"
$env:PATH = ("{1};{0}" -f $env:PATH, $dotnet)

./edge.cmd configSet -d $DeviceId -c $ConfigurationFilePath -l $iothub_connection
if ($LastExitCode)
{
    Throw "Error running deployment RES = $LastExitCode"
}
