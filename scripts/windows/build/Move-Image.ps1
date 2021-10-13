<#
 # This script moves a docker image from one registry to another registry.
 # It assumes that the caller is logged into both registries
 #>

param (
    [Parameter(Mandatory = $true)]
    [String]$FromImage,
    
    [Parameter(Mandatory = $true)]
    [String]$ToImage
)

Write-Host "Pulling $FromImage"
$PullCommand = "docker pull $FromImage"
Invoke-Expression $PullCommand
if ($LASTEXITCODE) {
    throw "'$PullCommand' failed with exit code $LASTEXITCODE"
}

Write-Host "Retag $FromImage $ToImage"
$RetagCommand = "docker tag $FromImage $ToImage"
Invoke-Expression $RetagCommand
if ($LASTEXITCODE) {
    throw "'$RetagCommand' failed with exit code $LASTEXITCODE"
}

Write-Host "Push $ToImage"
$PushCommand = "docker push $ToImage"
Invoke-Expression $PushCommand
if ($LASTEXITCODE) {
    throw "'$PushCommand' failed with exit code $LASTEXITCODE"
}