param(
    [String]$dotnet_cli_url= $(throw "You need to pass the URL of the .NET Core CLI Zip package."),
    [String]$nuget_url=$(throw "You need to pass the URL of the nuget exe.")
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

$baseFolder = "$env:AGENT_WORKFOLDER\nuget"
New-Item -ItemType Directory -Force -Path $baseFolder
$packageExe = "$baseFolder\nuget.exe"

$webclient = New-Object System.Net.WebClient
$webclient.DownloadFile($nuget_url,$packageExe)

$rootFolder = "$env:BUILD_REPOSITORY_LOCALPATH"
& $packageExe install OpenCover -version 4.6.519 -OutputDirectory $rootFolder
& $packageExe install OpenCoverToCoberturaConverter -version 0.2.6 -OutputDirectory $rootFolder
& $packageExe install ReportGenerator  -version 2.5.6 -OutputDirectory $rootFolder

Write-Host Cleaning up.
Remove-Item $packageZip

Write-Host Done!