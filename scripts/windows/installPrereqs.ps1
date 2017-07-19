param(
    [ValidateNotNullOrEmpty()]
    [String]$DotnetSdkUrl = "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-win-x64.zip",

    [ValidateNotNullOrEmpty()]
    [String]$NugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
)

# Installs the pre-reqs on the Windows machine.

if (-not (Test-Path env:AGENT_WORKFOLDER)) { throw "Environment variable AGENT_WORKFOLDER not set." }
if (-not (Test-Path env:BUILD_REPOSITORY_LOCALPATH)) { throw "Environment variable BUILD_REPOSITORY_LOCALPATH not set." }

$baseFolder = Join-Path -Path $env:AGENT_WORKFOLDER -ChildPath "dotnet"
if (Test-Path $baseFolder)
{
	Remove-Item $baseFolder -Force -Recurse
}

Write-Host "Downloading .Net Core package."
New-Item -ItemType Directory -Force -Path $baseFolder
$packageZip = Join-Path -Path $baseFolder -ChildPath "dotnet.zip"
$webclient = New-Object System.Net.WebClient
$webclient.DownloadFile($DotnetSdkUrl, $packageZip)

Write-Host "Extracting .Net Core package to $baseFolder folder."
Add-Type -A System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($packageZip, $baseFolder)

$baseFolder = Join-Path -Path $env:AGENT_WORKFOLDER -ChildPath "nuget"
New-Item -ItemType Directory -Force -Path $baseFolder
$packageExe = Join-Path -Path $baseFolder -ChildPath "nuget.exe"
$webclient = New-Object System.Net.WebClient
$webclient.DownloadFile($NugetUrl, $packageExe)

$rootFolder = $env:BUILD_REPOSITORY_LOCALPATH
& $packageExe install OpenCover -version 4.6.519 -OutputDirectory $rootFolder
& $packageExe install OpenCoverToCoberturaConverter -version 0.2.6 -OutputDirectory $rootFolder
& $packageExe install ReportGenerator  -version 2.5.6 -OutputDirectory $rootFolder

Write-Host "Cleaning up."
Remove-Item $packageZip

Write-Host "Installing Docker"
# Needs elevation
$installDockerScriptPath = Join-Path -Path (Split-Path $MyInvocation.MyCommand.Definition -Parent) -ChildPath "installDocker.ps1"
$Process = Start-Process "$PSHome\powershell.exe" -Wait -PassThru -Verb Runas `
    -ArgumentList ('-command "' + $installDockerScriptPath + '"')
if ((-not $Process) -or ($Process.ExitCode))
{
    throw "Failed docker installation with $($Process.ExitCode)"
}

Write-Host "Done!"