<#
 # This script is used to create artifact info file.
 #>
 
param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $OutputFolder,

    [ValidateNotNullOrEmpty()]
    [String] $BuildNumber = $(Throw "Build number is required")
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$artifactInfoFilePath = (Join-Path $OutputFolder "artifactInfo.txt")
"BuildNumber=$BuildNumber" | Tee-Object -FilePath $artifactInfoFilePath -Append
Write-Host "Created artifact info file in $artifactInfoFilePath"