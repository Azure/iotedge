<#
    .SYNOPSIS
        Powershell utility to setup environment for running E2E tests for Windows.

    .DESCRIPTION
        It is used to wrap all related steps to setup the environment for running E2E tests for Windows;
        It detects Windows IoT Core vs Windows Pro/Enterprise/Server and installs/updates/uninstalls iotedge service accordingly.

        To get details about parameters, please run "Get-Help .\Env-Setup.ps1 -Parameter *"

        Please ensure that E2E test folder have below folders/files:
        - artifacts\core-windows: artifact from Image build.
        - artifacts\iotedged-windows: artifact from edgelet build.
        - artifacts\packages: contains packages of Moby docker engine, CLI and IoT Edge security daemon.
          Either artifacts\iotedged-windows or packages folder exists, which is used for IoT Edge security daemon installation.

    .PARAMETER E2ETestFolder
        Path of E2E test folder which contains artifacts and certs folders; Default is current directory.

    .PARAMETER ArtifactImageBuildNumber
        Artifact image build number; it is used to construct path of docker images, pulling from docker registry. E.g. 20190101.1.

    .PARAMETER AttemptUpdate
        Switch controlling if an update of iotedge service should be attempted.

    .EXAMPLE
        .\Setup-Env.ps1
            -E2ETestFolder "C:\Data\e2etests"
            -ArtifactImageBuildNumber "20190101.1"
            -AttemptUpdate

    .NOTES
        This script is to setup the environment for running E2E tests.
        It detects and handles different Windows version differently.
        It triggers reboot on Windows IoT Core.
    #>

[CmdletBinding()]
Param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript({(Test-Path $_ -PathType Container)})]
    [string] $E2ETestFolder = ".",

    [ValidateNotNullOrEmpty()]
    [string] $ArtifactImageBuildNumber = $(Throw "Artifact image build number is required"),

    [switch] $AttemptUpdate
)

Function PrintHighlightedMessage
{
    param ([string] $heading)

    Write-Host -f Cyan $heading
}

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"
$global:ProgressPreference = "SilentlyContinue"


PrintHighlightedMessage "Environment Setup"

$osEdition = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').EditionID
Write-Host "Configuring OS edition $osEdition..."

$IoTEdgedArtifactFolder = Join-Path $E2ETestFolder "artifacts\iotedged-windows"
$InstallationScriptPath = Join-Path $E2ETestFolder "artifacts\core-windows\scripts\windows\setup\IotEdgeSecurityDaemon.ps1"
Invoke-Expression $InstallationScriptPath

If ($osEdition -eq "IoTUAP")    # Windows IoT Core - update iotedge
{
    # Check iotedge version
    $serviceName = 'iotedge'
    
    If (Get-Service $serviceName -ErrorAction SilentlyContinue) {
        $serviceVersion = Invoke-Expression "$serviceName version"
        Write-Host "Installed $serviceName service version: $serviceVersion"
        Write-Host "Artifact $serviceName service version: $ArtifactImageBuildNumber"
        If ((Get-Service $serviceName).Status -eq 'Running') {
            Stop-Service $serviceName
        }
        
        Write-Host "Cleanup existing containers..."
        try {
            $residualModules = $(docker -H npipe:////./pipe/iotedge_moby_engine ps -aq)
            if($residualModules.Length -gt 0) {
                docker -H npipe:////./pipe/iotedge_moby_engine rm -f $residualModules
            }
	    
	      docker -H npipe:////./pipe/iotedge_moby_engine system prune -a --volumes -f
        }
        catch {
            Write-Host "Cleanup existing containers failed."
			Write-Host $_.Exception.Message
        }

        # Delete iotedge config file
        $FileName = "$env:ProgramData\iotedge\config.yaml"
        if (Test-Path $FileName) 
        {
            Write-Host "Deleting $FileName..."
            Remove-Item $FileName
        }        
        
        if ($AttemptUpdate) {
            Write-Host "Attempt to update $serviceName..."
            try {
                # update triggers reboot
                Update-IoTEdge -ContainerOs Windows -OfflineInstallationPath $IoTEdgedArtifactFolder
            }
            catch {
                $testExitCode = [String]::Format("0x{0:X}", $LastExitCode)
                Write-Host "Update attempt unsuccessful (error code $testExitCode)."
                if ($testExitCode -eq "0x80188302") {
                    Write-Host "Package cannot be installed because it is already present on the image."
                }
                if ($testExitCode -eq "0x8018830D") {
                    Write-Host "A newer version is already installed on the device."
                }
		# sanity reboot on unsuccessful update
		shutdown -r -t 10
            }
        }
    } Else {
        Write-Host "Service $serviceName not found. Device is clean."
        # no need to do anything
    }

    # hide exit error caused by target reboot
    # next step in the pipeline will verify if the device comes back successfully
    Exit 0
}
Else    # Windows Pro/Enterprise/Server - uninstall iotedge
{
    Write-Host "Uninstall iotedged"
    # no reboot
    Uninstall-IoTEdge -Force
}
