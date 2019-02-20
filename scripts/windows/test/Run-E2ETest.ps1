<#
    .SYNOPSIS
        Powershell utility to streamline running E2E tests for Windows.

    .DESCRIPTION
        It is used to wrap all related steps to run E2E tests for Windows;
        it runs clean up, E2E test of given name, and print logs.
    
        To get details about parameters, please run "Get-Help .\Run-E2ETest.ps1 -Parameter *"
        To find out what E2E tests are supported, just run "Get-Help .\Run-E2ETest.ps1 -Parameter TestName"
        
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

    .PARAMETER TestName
        Name of E2E test to be run
        Note: Valid values are: 
            "All", "DirectMethodAmqp", "DirectMethodMqtt", "QuickstartCerts", "TempFilter", "TempFilterFunctions", "TempSensor", "TransparentGateway"

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
    [string] $E2ETestFolder = ".",

    [ValidateNotNullOrEmpty()]
    [string] $ReleaseLabel = $(Throw "Release label is required"),

    [ValidateNotNullOrEmpty()]
    [string] $ReleaseArtifactImageBuildNumber = $(Throw "Release artifact image build number is required"),

    [ValidateSet("All", "DirectMethodAmqp", "DirectMethodMqtt", "QuickstartCerts", "TempFilter", "TempFilterFunctions", "TempSensor", "TransparentGateway")]
    [string] $TestName = "All",

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
    PrintHighlightedMessage "Test Clean Up"
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
    Uninstall-SecurityDaemon -Force

    # This may require once IoT Edge created its only bridge network
    #Write-Host "Remove nat VM switch"
    #Remove-VMSwitch -Force 'nat' -ErrorAction SilentlyContinue
    #Write-Host "Restart Host Network Service"
    #Restart-Service -name hns
}

Function GetArchitecture
{
    $processorArchitecture = $ENV:PROCESSOR_ARCHITECTURE
    $Is64Bit = if ((Get-CimInstance -ClassName win32_operatingsystem).OSArchitecture.StartsWith("64")) { $True } Else { $False }
    
    If ($processorArchitecture.StartsWith("AMD") -And $Is64Bit)
    {
        Return "x64"
    }
    
    If ($processorArchitecture.StartsWith("ARM") -And -Not $Is64Bit)
    {
        Return "arm32v7"
    }
    
    Throw "Unsupported processor architecture $processorArchitecture (64-bit: $Is64Bit)"
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
    PrintHighlightedMessage "Prepare $TestWorkingFolder for test run"
    Remove-Item $TestWorkingFolder -Force -Recurse -ErrorAction SilentlyContinue
}

Function PrepareTestFromArtifacts
{
    PrintHighlightedMessage "Copy artifact files to $TestWorkingFolder"

    # IoT Edgelet
    If (Test-Path $PackagesArtifactFolder -PathType Container)
    {
        Write-Host "Copy packages artifact from $PackagesArtifactFolder to $PackagesWorkingFolder"
        Copy-Item $PackagesArtifactFolder -Destination $PackagesWorkingFolder -Recurse -Force
        Copy-Item $InstallationScriptPath -Destination $PackagesWorkingFolder -Force
    }
    ElseIf (Test-Path $IoTEdgedArtifactFolder -PathType Container)
    {
        Write-Host "Copy packages artifact from $IoTEdgedArtifactFolder to $PackagesWorkingFolder"
        Copy-Item $IoTEdgedArtifactFolder -Destination $IoTEdgedWorkingFolder -Recurse -Force
        Copy-Item $InstallationScriptPath -Destination $IoTEdgedWorkingFolder -Force
    }
    Else
    {
        Throw "Package and iotedged artifact folder doesn't exist"
    }

    # IoT Edge Quickstart
    Write-Host "Copy IoT Edge Quickstart from $IoTEdgeQuickstartArtifactFolder to $QuickstartWorkingFolder"
    Copy-Item $IoTEdgeQuickstartArtifactFolder -Destination $QuickstartWorkingFolder -Recurse -Force

    # Leaf device
    If (($TestName -eq "QuickstartCerts") -Or ($TestName -eq "TransparentGateway"))
    {
        Write-Host "Copy Leaf device from $LeafDeviceArtifactFolder to $LeafDeviceWorkingFolder"
        Copy-Item $LeafDeviceArtifactFolder -Destination $LeafDeviceWorkingFolder -Recurse -Force
    }

    # Deployment file
    If (($TestName -eq "DirectMethodAmqp") -Or
        ($TestName -eq "DirectMethodMqtt") -Or
        ($TestName -eq "TempFilter") -Or
        ($TestName -eq "TempFilterFunctions"))
    {
        Switch ($TestName)
        {
            "DirectMethodAmqp"
            {
                Write-Host "Copy deployment file from $DirectMethodModuleToModuleDeploymentArtifactFilePath"
                Copy-Item $DirectMethodModuleToModuleDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
                (Get-Content $DeploymentWorkingFilePath).replace('<UpstreamProtocol>','Amqp') | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<ClientTransportType>','Amqp_Tcp_Only') | Set-Content $DeploymentWorkingFilePath
            }
            "DirectMethodMqtt"
            {
                Write-Host "Copy deployment file from $DirectMethodModuleToModuleDeploymentArtifactFilePath"
                Copy-Item $DirectMethodModuleToModuleDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
                (Get-Content $DeploymentWorkingFilePath).replace('<UpstreamProtocol>','Mqtt') | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<ClientTransportType>','Mqtt_Tcp_Only') | Set-Content $DeploymentWorkingFilePath

                If ($Architecture -eq "arm32v7")
                {
                    (Get-Content $DeploymentWorkingFilePath).replace('<MqttEventsProcessorThreadCount>','1') | Set-Content $DeploymentWorkingFilePath
                }
            }
            "TempFilter"
            {
                Write-Host "Copy deployment file from $ModuleToModuleDeploymentArtifactFilePath"
                Copy-Item $ModuleToModuleDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
            "TempFilterFunctions"
            {
                Write-Host "Copy deployment file from $ModuleToFunctionDeploymentArtifactFilePath"
                Copy-Item $ModuleToFunctionDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
        }

        $ImageArchitectureLabel = $(GetImageArchitectureLabel)
        (Get-Content $DeploymentWorkingFilePath).replace('<Architecture>', $ImageArchitectureLabel) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<OptimizeForPerformance>', 'true') | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<Build.BuildNumber>', $ReleaseArtifactImageBuildNumber) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<CR.Username>', $ContainerRegistryUsername) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<CR.Password>', $ContainerRegistryPassword) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('-linux-', '-windows-') | Set-Content $DeploymentWorkingFilePath
    }
}

Function PrintLogs
{
    Param (
        [Datetime] $testStartTime,
        [int] $testExitCode
        )

    $now = Get-Date
    PrintHighlightedMessage "Test finished with exit code $testExitCode at $now, took $($now - $testStartTime)"

    If ($testExitCode -eq 0)
    {
        Return
    }

    # Need to use Get-WinEvent, since Get-EventLog is not supported in Windows IoT Core ARM
    Get-WinEvent -ea SilentlyContinue `
        -FilterHashtable @{ProviderName= "iotedged";
        LogName = "application"; StartTime = $testStartTime} |
	    select TimeCreated, Message | 
	    sort-object @{Expression="TimeCreated";Descending=$false} |
        format-table -autosize -wrap | Out-Host

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

    if (($TestName -eq "TempSensor") -Or `
        ($TestName -eq "TempFilter") -Or `
        ($TestName -eq "TempFilterFunctions"))
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

    if ($TestName -eq "TempFilter")
    {
        Try {
            Write-Host "TEMP FILTER LOGS"
            Invoke-Expression "$dockerCmd logs tempFilter"
        }
        Catch
        {
            Write-Host "Cannot output Temp Filter logs"
        }
    }

    if ($TestName -eq "TempFilterFunctions")
    {
        Try {
            Write-Host "TEMP FILTER FUNCTIONS LOGS"
            Invoke-Expression "$dockerCmd logs tempFilterFunctions"
        }
        Catch
        {
            Write-Host "Cannot output Temp Filter Functions logs"
        }
    }
}

Function RunAllTests
{
    $TestName = "DirectMethodAmqp"
    $lastTestExitCode = RunDirectMethodAmqpTest
    
    $TestName = "DirectMethodMqtt"
    $testExitCode = RunDirectMethodMqttTest
    $lastTestExitCode = If ($testExitCode -gt 0) { $testExitCode } Else { $lastTestExitCode }
    
    $TestName = "QuickstartCerts"
    $testExitCode = RunQuickstartCertsTest
    $lastTestExitCode = If ($testExitCode -gt 0) { $testExitCode } Else { $lastTestExitCode }
    
    $TestName = "TempFilter"
    $testExitCode = RunTempFilterTest
    $lastTestExitCode = If ($testExitCode -gt 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "TempFilterFunctions"
    $testExitCode = RunTempFilterFunctionsTest
    $lastTestExitCode = If ($testExitCode -gt 0) { $testExitCode } Else { $lastTestExitCode }
    
    $TestName = "TempSensor"
    $testExitCode = RunTempSensorTest
    $lastTestExitCode = If ($testExitCode -gt 0) { $testExitCode } Else { $lastTestExitCode }
    
    $TestName = "TransparentGateway"
    $testExitCode = RunTransparentGatewayTest
    $lastTestExitCode = If ($testExitCode -gt 0) { $testExitCode } Else { $lastTestExitCode }
    
    Return $lastTestExitCode
}

Function RunDirectMethodAmqpTest
{
    PrintHighlightedMessage "Run Direct Method Amqp test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-DMAmqp"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" and deployment file $DeploymentWorkingFilePath started at $testStartAt"
    
    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunDirectMethodMqttTest
{
    PrintHighlightedMessage "Run Direct Method Mqtt test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-DMMqtt"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" and deployment file $DeploymentWorkingFilePath started at $testStartAt"
    
    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunQuickstartCertsTest
{
    PrintHighlightedMessage "Run Quickstart Certs test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-QuickstartCerts"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"doesNotNeed`" ``
        -n `"$env:computername`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" --optimize_for_performance true ``
        -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        --leave-running=Core ``
        --no-verify"
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host

    $caCertPath = (Get-ChildItem C:\ProgramData\iotedge\hsm\certs\edge_owner_ca*.pem | Select -First 1).FullName
    Write-Host "CA certificate path=$caCertPath"

    Write-Host "Run LeafDevice"
    &$LeafDeviceExeTestPath `
        -d "${deviceId}-leaf" `
        -c "$IoTHubConnectionString" `
        -e "$EventHubConnectionString" `
        -ct "$caCertPath" `
        -ed "$env:computername" | Out-Host
    $testExitCode = $LastExitCode
    
    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunTempFilterTest
{
    PrintHighlightedMessage "Run TempFilter test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-tempFilter"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" and deployment file $DeploymentWorkingFilePath started at $testStartAt"
    
    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilter`" ``
            -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunTempFilterFunctionsTest
{
    if ($Architecture -eq "arm32v7")
    {
        PrintHighlightedMessage "Temp Filter Functions test is not supported on $Architecture"
        Return 0
    }

    PrintHighlightedMessage "Run TempFilterFunctions test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-tempFilterFunc"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" and deployment file $DeploymentWorkingFilePath started at $testStartAt"
    
    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilterFunctions`" ``
            -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunTempSensorTest
{
    PrintHighlightedMessage "Run TempSensor test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-tempSensor"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt."

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"$EventHubConnectionString`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" --optimize_for_performance true ``
        -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        -tw `"$TwinTestFileArtifactFilePath`""
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunTransparentGatewayTest
{
    PrintHighlightedMessage "Run Transparent Gateway test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-TransGW"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt."

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"doesNotNeed`" ``
        -n `"$env:computername`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" --optimize_for_performance true ``
        -t `"${ReleaseArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        --leave-running=Core ``
        --no-verify ``
        --device_ca_cert `"$DeviceCACertificatePath`" ``
        --device_ca_pk `"$DeviceCAPrimaryKeyPath`" ``
        --trusted_ca_certs `"$TrustedCACertificatePath`""
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host

    Write-Host "Run LeafDevice"
    &$LeafDeviceExeTestPath `
        -d "${deviceId}-leaf" `
        -c "$IoTHubConnectionString" `
        -e "$EventHubConnectionString" `
        -ct "$TrustedCACertificatePath" `
        -ed "$env:computername" | Out-Host
    $testExitCode = $LastExitCode
    
    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunTest
{
    $testExitCode = 0
    
    Switch ($TestName)
    {
        "All" { $testExitCode = RunAllTests; break }
        "DirectMethodAmqp" { $testExitCode = RunDirectMethodAmqpTest; break }
        "DirectMethodMqtt" { $testExitCode = RunDirectMethodMqttTest; break }
        "QuickstartCerts" { $testExitCode = RunQuickstartCertsTest; break }
        "TempFilter" { $testExitCode = RunTempFilterTest; break }
        "TempFilterFunctions" { $testExitCode = RunTempFilterFunctionsTest; break }
        "TempSensor" { $testExitCode = RunTempSensorTest; break }
        "TransparentGateway" { $testExitCode = RunTransparentGatewayTest; break }
		default { Throw "$TestName test is not supported." }
    }

    Exit $testExitCode -gt 0
}

Function TestSetup
{
    ValidateE2ETestParameters
    CleanUp | Out-Host
    InitializeWorkingFolder
    PrepareTestFromArtifacts
}

Function ValidateE2ETestParameters
{
    PrintHighlightedMessage "Validate E2E test parameters for $TestName"

    If (-Not((Test-Path (Join-Path $IoTEdgedArtifactFolder "*")) -Or (Test-Path (Join-Path $PackagesArtifactFolder "*"))))
    {
        Throw "Either $IoTEdgedArtifactFolder or $PackagesArtifactFolder should exist"
    }

    $validatingItems = @(
        (Join-Path $IoTEdgeQuickstartArtifactFolder "*"),
        $InstallationScriptPath)

    If (($TestName -eq "DirectMethodAmqp") -Or ($TestName -eq "DirectMethodMqtt"))
    {
        $validatingItems += $DirectMethodModuleToModuleDeploymentArtifactFilePath
    }

    If (($TestName -eq "QuickstartCerts") -Or ($TestName -eq "TransparentGateway"))
    {
        $validatingItems += (Join-Path $LeafDeviceArtifactFolder "*")
    }

    If ($TestName -eq "TempFilter")
    {
        $validatingItems += $ModuleToModuleDeploymentArtifactFilePath
    }

    If ($TestName -eq "TempFilterFunctions")
    {
        $validatingItems += $ModuleToFunctionDeploymentArtifactFilePath
    }

    If ($TestName -eq "TempSensor")
    {
        $validatingItems += $TwinTestFileArtifactFilePath
    }

    If ($TestName -eq "TransparentGateway")
    {
        $validatingItems += $DeviceCACertificatePath
        $validatingItems += $DeviceCAPrimaryKeyPath
        $validatingItems += $TrustedCACertificatePath
    }

    $validatingItems | ForEach-Object {
        If (-Not (Test-Path $_))
        {
            Throw "$_ is not found or it is empty"
        }
    }
}

Function PrintHighlightedMessage
{
    param ([string] $heading)

    Write-Host -f Cyan $heading
}

$Architecture = GetArchitecture
$ContainerRegistry = "edgebuilds.azurecr.io"
$E2ETestFolder = (Resolve-Path $E2ETestFolder).Path
$InstallationScriptPath = Join-Path $E2ETestFolder "artifacts\core-windows\scripts\windows\setup\IotEdgeSecurityDaemon.ps1"
$ModuleToModuleDeploymentFilename = "module_to_module_deployment.template.json"
$ModuleToFunctionsDeploymentFilename = "module_to_functions_deployment.template.json"
$DirectMethodModuleToModuleDeploymentFilename = "dm_module_to_module_deployment.json"
$TwinTestFilename = "twin_test_tempSensor.json"

$IoTEdgeQuickstartArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\IoTEdgeQuickstart\$Architecture"
$LeafDeviceArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\LeafDevice\$Architecture"
$IoTEdgedArtifactFolder = Join-Path $E2ETestFolder "artifacts\iotedged-windows"
$PackagesArtifactFolder = Join-Path $E2ETestFolder "artifacts\packages"
$DeploymentFilesFolder = Join-Path $E2ETestFolder "artifacts\core-windows\e2e_deployment_files"
$TestFileFolder = Join-Path $E2ETestFolder "artifacts\core-windows\e2e_test_files"
$ModuleToModuleDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $ModuleToModuleDeploymentFilename
$ModuleToFunctionDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $ModuleToFunctionsDeploymentFilename
$TwinTestFileArtifactFilePath = Join-Path $TestFileFolder $TwinTestFilename
$DirectMethodModuleToModuleDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $DirectMethodModuleToModuleDeploymentFilename

$TestWorkingFolder = Join-Path $E2ETestFolder "working"
$QuickstartWorkingFolder = (Join-Path $TestWorkingFolder "quickstart")
$LeafDeviceWorkingFolder = (Join-Path $TestWorkingFolder "leafdevice")
$IoTEdgedWorkingFolder = (Join-Path $TestWorkingFolder "iotedged")
$PackagesWorkingFolder = (Join-Path $TestWorkingFolder "packages")
$IoTEdgeQuickstartExeTestPath = (Join-Path $QuickstartWorkingFolder "IotEdgeQuickstart.exe")
$LeafDeviceExeTestPath = (Join-Path $LeafDeviceWorkingFolder "LeafDevice.exe")
$DeploymentWorkingFilePath = Join-Path $QuickstartWorkingFolder "deployment.json"

$DeviceCACertificatePath = (Join-Path $E2ETestFolder "certs\new-edge-device-full-chain.cert.pem")
$DeviceCAPrimaryKeyPath = (Join-Path $E2ETestFolder "certs\private\new-edge-device.key.pem")
$TrustedCACertificatePath = (Join-Path $E2ETestFolder "certs\azure-iot-test-only.root.ca.cert.pem")

&$InstallationScriptPath
RunTest