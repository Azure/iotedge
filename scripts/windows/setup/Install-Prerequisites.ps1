<#
 # Installs .NET Core, Nuget, and Python.
 #>

param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Invoke-WebRequest $_ -DisableKeepAlive -UseBasicParsing -Method "Head"})]
    [String]$DotnetSdkUrl = "https://download.microsoft.com/download/D/C/F/DCFA73BE-93CE-4DA0-AB76-98972FD6E475/dotnet-sdk-2.1.101-win-x64.zip",

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Invoke-WebRequest $_ -DisableKeepAlive -UseBasicParsing -Method "Head"})]
    [String]$NugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe",

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Invoke-WebRequest $_ -DisableKeepAlive -UseBasicParsing -Method "Head"})]
    [String]$PythonUrl = "https://www.python.org/ftp/python/2.7.14/python-2.7.14.msi",

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String]$AgentWorkFolder = $Env:AGENT_WORKFOLDER,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String]$BuildRepositoryLocalPath = $Env:BUILD_REPOSITORY_LOCALPATH,

    [Switch]$Dotnet,
    [Switch]$Python,
    [Switch]$Nuget
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

<#
 # Prepare environment
 #>

Import-Module ([IO.Path]::Combine($PSScriptRoot, "..", "Defaults.psm1")) -Force

if (-not $AgentWorkFolder) {
    $AgentWorkFolder = DefaultAgentWorkFolder
}

if (-not $BuildRepositoryLocalPath) {
    $BuildRepositoryLocalPath = DefaultBuildRepositoryLocalPath
}

$All = -not $Dotnet -and -not $Python -and -not $Nuget -and -not $Coverage

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

<#
 # Install .NET Core
 #>

if ($Dotnet -or $All) {
    $DotnetInstallPath = Join-Path $AgentWorkFolder "dotnet"
    if (Test-Path $DotnetInstallPath) {
        Remove-Item $DotnetInstallPath -Force -Recurse
    }
    New-Item $DotnetInstallPath -ItemType "Directory" -Force

    Write-Host "Downloading .NET Core package."
    $DotnetZip = Join-Path $Env:TEMP "dotnet.zip"
    (New-Object System.Net.WebClient).DownloadFile($DotnetSdkUrl, $DotnetZip)

    Write-Host "Extracting .NET Core package to $DotnetInstallPath."
    Add-Type -A System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($DotnetZip, $DotnetInstallPath)

    Write-Host "Cleaning up .NET Core installation."
    Remove-Item $DotnetZip
}

<#
 # Install Nuget 
 #>

$NugetInstallPath = Join-Path $AgentWorkFolder "nuget"
$NugetExe = Join-Path $NugetInstallPath "nuget.exe"
if ($Nuget -or $All) {
    if (Test-Path $NugetInstallPath) {
        Remove-Item $NugetInstalLPath -Force -Recurse
    }
    New-Item $NugetInstallPath -ItemType "Directory" -Force

    Write-Host "Downloading Nuget."
    (New-Object System.Net.WebClient).DownloadFile($NugetUrl, $NugetExe)
}

<#
 # Install Python 
 #>
if ($Python -or $All) {
    $PythonInstallPath = Join-Path $AgentWorkFolder "python"
    if (Test-Path $PythonInstallPath) {
        Remove-Item $PythonInstallPath -Force -Recurse
    }
    New-Item $PythonInstallPath -ItemType "Directory" -Force

    Write-Host "Downloading Python."
    $PythonMsi = Join-Path $PythonInstallPath "python.msi"
    (New-Object System.Net.WebClient).DownloadFile($PythonUrl, $PythonMsi)
    cmd /c start /wait msiexec /passive /package $PythonMsi 
}

Write-Host "Done!"
