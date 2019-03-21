<#
 # Builds and publishes to target/publish/ all .NET Core solutions in the repo
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
"BuildNumber=$BuildNumber" | Tee-Object -FilePath (Join-Path $path "artifactInfo.txt") -Append
Write-Host "Published artifact info file to $artifactInfoFilePath"