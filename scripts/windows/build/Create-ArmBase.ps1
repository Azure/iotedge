<#
 # This script creates an ARM docker image as a base for EdgeAgent, EdgeHub, and general module.
 # then pushes it to the appropriate registries.
 # It assumes that the caller is logged into registries:
 # edgebuilds.azurecr.io
 # edgerelease.azurecr.io
 # hub.docker.com
 #>
 
param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $ProjectDirectory,
    
    [ValidateNotNullOrEmpty()]
    [String] $DockerNamespace,
    
    [ValidateSet("azureiotedge-module-base", "azureiotedge-hub-base", "azureiotedge-agent-base")]
    [String] $DockerImageName,
    
    [ValidateNotNullOrEmpty()]
    [String] $DockerImageVersion,

    [Switch] $NoPush
)

$DockerImageArchitecture = "arm32v7"

<#
 # Docker file
 #>
$DockerFile = "$ProjectDirectory/docker/windows/$DockerImageArchitecture/base/Dockerfile"
if (-Not (Test-Path $DockerFile -PathType Leaf))
{
    throw "$DockerFile not found"
}

<#
 # Docker registries
 #>
$DockerRegistries = @("edgebuilds.azurecr.io/", "edgerelease.azurecr.io/")

<#
 # Docker image tags
 #>
$DockerImageTags = New-Object 'System.Collections.Generic.List[String]'
foreach($Registry in $DockerRegistries)
{
    $ImageTag = "$Registry$DockerNamespace/${DockerImageName}:$DockerImageVersion-windows-$DockerImageArchitecture"
    $DockerImageTags.Add($ImageTag) 
}

<#
 # Docker build
 #>
foreach($ImageTag in $DockerImageTags)
{
    Write-Host "Docker build image tag [$ImageTag]"
    
    $docker_build_cmd = "docker build --no-cache"
    $docker_build_cmd += " --tag $ImageTag"
    $docker_build_cmd +=" --file $DockerFile"
    $docker_build_cmd +=" $ProjectDirectory"
    
    Write-Host "Command: $docker_build_cmd"
    Invoke-Expression $docker_build_cmd
}

if (-Not $NoPush)
{
    foreach($ImageTag in $DockerImageTags)
    {
        Write-Host "Command: docker push $ImageTag"
        docker push $ImageTag
    }
}