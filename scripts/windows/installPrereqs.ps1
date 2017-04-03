param(
    [String]$dotnet_cli_url= $(throw "You need to pass the URL of the .NET Core CLI Zip package.")
)

# Installs the pre-reqs (currently only .Net Core) on the Windows machine.

if (-not (Test-Path env:AGENT_WORKFOLDER)) { $(throw "Environment variable AGENT_WORKFOLDER not set.") }
$baseFolder = "$env:AGENT_WORKFOLDER\dotnet"
if (Test-Path $baseFolder){
	Remove-Item $baseFolder -Force -Recurse
}

Write-Host Downloading .Net Core package.
New-Item -ItemType Directory -Force -Path $baseFolder
$packageZip = "$baseFolder\dotnet.zip"

$webclient = New-Object System.Net.WebClient
$webclient.DownloadFile($dotnet_cli_url,$packageZip)

Write-Host Extracting .Net Core package to $baseFolder folder.
Add-Type -A System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($packageZip, $baseFolder)

Write-Host Cleaning up.
Remove-Item $packageZip

Write-Host Done!