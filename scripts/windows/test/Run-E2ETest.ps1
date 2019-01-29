<#
    .SYNOPSIS
        It is used to streamline running E2E tests for Windows.

    .DESCRIPTION
        It is used to wrap all related steps to run E2E tests for Windows;
        it runs clean up, E2E test of given name, and print logs.

        Please ensure that E2E test folder have below folders/files:
        - artifacts\core-windows: artifact from Image build.
        - artifacts\iotedged-windows: artifact from edgelet build.
        - artifacts\packages: contains packages of Moby docker engine, CLI and IoT Edge security daemon.
          Either artifacts\iotedged-windows or packages folder exists, which is used for IoT Edge security daemon installation.
        - certs: contains CA certs for certificate-related testing.

    .PARAMETER E2ETestFolder
        Path of E2E test folder

    .PARAMETER ReleaseLabel
        Release label, can be uniquely identify the build (e.g <ReleaseName>-<ReleaseAttempt>); which is used as part of Edge device name.

    .PARAMETER ReleaseArtifactImageBuildNumber
        Release artifact image build number; it is used to construct path of docker images, pulling from EdgeBuilds docker registry. E.g. 20190101.1.

    .PARAMETER Architecture
        Runtime architecture
        Note: Valid values are "x64" and "arm32v7".

    .PARAMETER TestName
        Name of E2E test to be run
        Note: Valid values are: "TempSensor", "TempFilter".

    .PARAMETER ContainerRegistryUserName
        Username of container registry

    .PARAMETER ContainerRegistryPassword
        Password of given username for container registory

    .PARAMETER IoTHubConnectionString
        IoT hub connection string for creating edge device

    .PARAMETER EventHubConnectionString
        Event hub connection string for receive D2C messages

    .EXAMPLE
        .\Run-E2ETest.ps1 -E2ETestFolder "C:\Data\e2etests" -ReleaseLabel "Release-ARM-1" -ReleaseArtifactImageBuildNumber "20190101.1" -Architecture "x64" -TestName "TempSensor" -ContainerRegistryUsername "EdgeBuilds" -ContainerRegistryPassword "xxxx" -IoTHubConnectionString "xxxx" -EventHubConnectionString "xxxx"

    .NOTES
        This script is to make running E2E tests easier and centralize E2E test steps in 1 place for reusability.
        It shares common tasks such as clean up and installation of IoT Edge Security Daemon.
        Each test should have its own function to implement test steps.
    #>

[CmdletBinding()]
Param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript({(Test-Path $_ -PathType Container)})]
    [string] $E2ETestFolder = $(Throw "Path of E2ETest folder is missing or invalid"),

    [ValidateNotNullOrEmpty()]
    [string] $ReleaseLabel = $(Throw "Release label is required"),

    [ValidateNotNullOrEmpty()]
    [string] $ReleaseArtifactImageBuildNumber = $(Throw "Release artifact image build number is required"),

    [ValidateSet("x64", "arm32v7")]
    [string] $Architecture = $(Throw "Architecture is required"),

    [ValidateSet("TempSensor", "TempFilter")]
    [string] $TestName = $(Throw "Test name is required"),

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistryUsername = $(Throw "Container registry username is required"),

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistryPassword = $(Throw "Container registry password is required"),

    [ValidateNotNullOrEmpty()]
    [string] $IoTHubConnectionString = $(Throw "IoT hub connection string is required"),

    [ValidateNotNullOrEmpty()]
    [string] $EventHubConnectionString = $(Throw "Event hub connection string is required")
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

Function AppendInstallationOption([string] $testCommand)
{
    If (Test-Path (Join-Path $PackagesWorkingFolder "*"))
    {
        Return $testCommand + " --offline-installation-path `"$PackagesWorkingFolder`""
    }

    Return $testCommand += " -a `"$IoTEdgedWorkingFolder`""
}

Function CleanUp
{
    WriteHeading "Test Clean Up"
    Write-Host "Do IoT Edge Moby system prune"

    Try 
    {
        docker -H npipe:////./pipe/iotedge_moby_engine system prune -f
    }
    Catch
    {
      # Ignore error and just print it out
      Write-Verbose "$_"
    }

    Write-Host "Uninstall iotedged"
    Uninstall-SecurityDaemon -Force -DeleteConfig -DeleteMobyDataRoot

    # This may require once IoT Edge created its only bridge network
    #Write-Host "Remove nat VM switch"
    #Remove-VMSwitch -Force 'nat' -ErrorAction SilentlyContinue
    #Write-Host "Restart Host Network Service"
    #Restart-Service -name hns
}

Function GetImageArchitectureLabel
{
    Switch ($Architecture)
    {
        "x64" { Return "amd64" }
        "arm32v7" {Return "arm32v7" }
    }

    Throw "Can't find image architecture label for $Architecture"
}

Function InitializeWorkingFolder
{
    Write-Host "Prepare $TestWorkingFolder for test run"
    Remove-Item $TestWorkingFolder -Force -Recurse -ErrorAction SilentlyContinue
}

Function PrepareTestFromArtifacts
{
    Write-Host "Copy files to $TestWorkingFolder"
    Copy-Item $IoTEdgeQuickstartArtifactFolder -Destination $QuickstartWorkingFolder -Recurse -Force

    If ($TestName -eq "TempFilter")
    {
        $ImageArchitectureLabel = $(GetImageArchitectureLabel)
        Copy-Item $ModuleToModuleDeploymentArtifactsFilePath -Destination $ModuleToModuleDeploymentWorkingFilePath -Force
        (Get-Content $ModuleToModuleDeploymentWorkingFilePath).replace('<Architecture>',$ImageArchitectureLabel) | Set-Content $ModuleToModuleDeploymentWorkingFilePath
        (Get-Content $ModuleToModuleDeploymentWorkingFilePath).replace('<OptimizeForPerformance>','true') | Set-Content $ModuleToModuleDeploymentWorkingFilePath
        (Get-Content $ModuleToModuleDeploymentWorkingFilePath).replace('<Build.BuildNumber>',$ReleaseArtifactImageBuildNumber) | Set-Content $ModuleToModuleDeploymentWorkingFilePath
        (Get-Content $ModuleToModuleDeploymentWorkingFilePath).replace('<CR.Username>',$ContainerRegistryUsername) | Set-Content $ModuleToModuleDeploymentWorkingFilePath
        (Get-Content $ModuleToModuleDeploymentWorkingFilePath).replace('<CR.Password>',$ContainerRegistryPassword) | Set-Content $ModuleToModuleDeploymentWorkingFilePath
        (Get-Content $ModuleToModuleDeploymentWorkingFilePath).replace('-linux-','-windows-') | Set-Content $ModuleToModuleDeploymentWorkingFilePath
    }

    If (Test-Path $PackagesArtifactFolder -PathType Container)
    {
        Copy-Item $PackagesArtifactFolder -Destination $PackagesWorkingFolder -Recurse -Force
        Copy-Item $InstallationScriptPath -Destination $PackagesWorkingFolder -Force
    }
    ElseIf (Test-Path $IoTEdgedArtifactFolder -PathType Container)
    {
        Copy-Item $IoTEdgedArtifactFolder -Destination $IoTEdgedWorkingFolder -Recurse -Force
        Copy-Item $InstallationScriptPath -Destination $IoTEdgedWorkingFolder -Force
    }
    Else
    {
        Throw "Package and iotedged artifact folder doesn't exist"
    }
}

Function PrintLogs
{
    Param ([Datetime] $testStartTime)

    # Need to use Get-WinEvent, since Get-EventLog is not supported in Windows IoT Core ARM
    Get-WinEvent -ea SilentlyContinue `
        -FilterHashtable @{ProviderName= "iotedged";
        LogName = "application"; StartTime = $testStartTime} |
	    select TimeCreated, Message | 
	    sort-object @{Expression="TimeCreated";Descending=$false} |
        format-table -autosize -wrap

    $dockerCmd ="docker -H npipe:////./pipe/iotedge_moby_engine"

    Try
    {
        Write-Host "EDGE AGENT LOGS"
        Invoke-Expression "$dockerCmd logs edgeAgent"
    } 
    Catch
    {
        Write-Host "Exception caught when output Edge Agent logs"
    }

    Try
    {
        Write-Host "EDGE HUB LOGS"
        Invoke-Expression "$dockerCmd logs edgeHub"
    } 
    Catch
    {
        Write-Host "Exception caught when output Edge Hub logs"
    }

    if ($TestName -eq "TempSensor")
    {
        Try
        {
            Write-Host "TEMP SENSOR LOGS"
            Invoke-Expression "$dockerCmd logs tempSensor"
        } 
        Catch
        {
            Write-Host "Exception caught when output Temp Sensor logs"
        }
    }
}

Function RunTempFilterTest
{
    WriteHeading "Run TempFilter test"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-tempFilter"
    Write-Host "Run quickstart test with -d ""$deviceId"" and deployment file $ModuleToModuleDeploymentWorkingFilePath."
    
    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"edgebuilds.azurecr.io`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilter`" ``
            -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$ModuleToModuleDeploymentWorkingFilePath`""
    $testCommand = AppendInstallationOption($testCommand)

    Invoke-Expression $testCommand
    $testExitCode = $lastExitCode
    PrintLogs($testStartAt)
    exit $testExitCode    
}

Function RunTempSensorTest
{
    WriteHeading "Run TempSensor test"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-tempSensor"
    Write-Host "Run quickstart test with -d ""$deviceId""."

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"$EventHubConnectionString`" ``
        -r `"edgebuilds.azurecr.io`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" --optimize_for_performance true ``
        -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`""
    $testCommand = AppendInstallationOption($testCommand)

    Invoke-Expression $testCommand
    $testExitCode = $lastExitCode
    PrintLogs($testStartAt)
    exit $testExitCode    
}

Function RunTest
{
    Switch ($TestName)
    {
        "TempSensor" { RunTempSensorTest; break }
        "TempFilter" { RunTempFilterTest; break }
		default { Throw "$TestName test is not supported." }
    }
}

Function TestSetup
{
    CleanUp
    InitializeWorkingFolder
    PrepareTestFromArtifacts
}

Function ValidateE2ETestParameters
{
    WriteHeading "Validate E2E test parameters"

    $validatingItems = @(
        (Join-Path $IoTEdgeQuickstartArtifactFolder "*"),
        (Join-Path $LeafDeviceArtifactFolder "*"),
        $InstallationScriptPath)

    $validatingItems | ForEach-Object {
        If (-Not (Test-Path $_))
        {
            Throw "$_ is not found or it is empty"
        }
    }

    If (-Not((Test-Path (Join-Path $IoTEdgedArtifactFolder "*")) -Or (Test-Path (Join-Path $PackagesArtifactFolder "*"))))
    {
        Throw "Either $IoTEdgedArtifactFolder or $PackagesArtifactFolder should exist"
    }
}

Function WriteHeading
{
    param ([string] $heading)

    Write-Host -f Cyan $heading
}

$E2ETestFolder = (Resolve-Path $E2ETestFolder).Path
$InstallationScriptPath = Join-Path $E2ETestFolder "artifacts\core-windows\scripts\windows\setup\IotEdgeSecurityDaemon.ps1"
$ModuleToModuleDeploymentFilename = "module_to_module_deployment.template.json"

$IoTEdgeQuickstartArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\IoTEdgeQuickstart\$Architecture"
$LeafDeviceArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\LeafDevice\$Architecture"
$IoTEdgedArtifactFolder = Join-Path $E2ETestFolder "artifacts\iotedged-windows"
$PackagesArtifactFolder = Join-Path $E2ETestFolder "artifacts\packages"
$DeploymentFilesFolder = Join-Path $E2ETestFolder "artifacts\core-windows\e2e_deployment_files"
$ModuleToModuleDeploymentArtifactsFilePath = Join-Path $DeploymentFilesFolder $ModuleToModuleDeploymentFilename

$TestWorkingFolder = Join-Path $E2ETestFolder "working"
$QuickstartWorkingFolder = (Join-Path $TestWorkingFolder "quickstart")
$IoTEdgedWorkingFolder = (Join-Path $TestWorkingFolder "iotedged")
$PackagesWorkingFolder = (Join-Path $TestWorkingFolder "packages")
$IoTEdgeQuickstartExeTestPath = (Join-Path $QuickstartWorkingFolder "IotEdgeQuickstart.exe")
$ModuleToModuleDeploymentWorkingFilePath = Join-Path $QuickstartWorkingFolder $ModuleToModuleDeploymentFilename

ValidateE2ETestParameters
RunTest