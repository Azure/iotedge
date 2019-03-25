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

    .PARAMETER E2ETestFolder
        Path of E2E test folder which contains artifacts and certs folders; Default is current directory.

    .PARAMETER ReleaseLabel
        Release label, can be uniquely identify the build (e.g <ReleaseName>-<ReleaseAttempt>); which is used as part of Edge device name.

    .PARAMETER ArtifactImageBuildNumber
        Artifact image build number; it is used to construct path of docker images, pulling from docker registry. E.g. 20190101.1.

    .PARAMETER TestName
        Name of E2E test to be run
        Note: Valid values are:
            "All", "DirectMethodAmqp", "DirectMethodMqtt", "QuickstartCerts", "TempFilter", "TempFilterFunctions", "TempSensor", "TransparentGateway"

    .PARAMETER ContainerRegistry
        Host address of container registry; Default is "edgebuilds.azurecr.io".

    .PARAMETER ContainerRegistryUsername
        Username of container registry; Default is "EdgeBuilds".

    .PARAMETER ContainerRegistryPassword
        Password of given username for container registory

    .PARAMETER IoTHubConnectionString
        IoT hub connection string for creating edge device

    .PARAMETER EventHubConnectionString
        Event hub connection string for receive D2C messages

    .PARAMETER ProxyUri
        (Optional) The URI of an HTTPS proxy server; if specified, all communications to IoT Hub will go through this proxy.

    .EXAMPLE
        .\Run-E2ETest.ps1
            -E2ETestFolder "C:\Data\e2etests"
            -ReleaseLabel "Release-ARM-1"
            -ArtifactImageBuildNumber "20190101.1"
            -TestName "TempSensor"
            -ContainerRegistry "edgebuilds.azurecr.io"
            -ContainerRegistryUsername "EdgeBuilds"
            -ContainerRegistryPassword "xxxx"
            -IoTHubConnectionString "xxxx"
            -EventHubConnectionString "xxxx"
            -ProxyUri "http://proxyserver:3128"

        Transparent gateway test command with custom Edge device certificates:
        .\Run-E2ETest.ps1
            -E2ETestFolder "C:\Data\e2etests"
            -ReleaseLabel "Release-ARM-1"
            -ArtifactImageBuildNumber "20190101.1"
            -TestName "TransparentGateway"
            -ContainerRegistry "edgebuilds.azurecr.io"
            -ContainerRegistryUsername "EdgeBuilds"
            -ContainerRegistryPassword "xxxx"
            -IoTHubConnectionString "xxxx"
            -EventHubConnectionString "xxxx"
            -ProxyUri "http://proxyserver:3128"
            -EdgeE2ERootCACertRSAFile "file path"  #if not provided, a default path will be checked
            -EdgeE2ERootCAKeyRSAFile "file path"   #if not provided, a default path will be checked
            -EdgeE2ETestRootCAPassword "xxxx"

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
    [string] $ArtifactImageBuildNumber = $(Throw "Artifact image build number is required"),

    [ValidateSet("All", "DirectMethodAmqp", "DirectMethodMqtt", "QuickstartCerts", "TempFilter", "TempFilterFunctions", "TempSensor", "TransparentGateway")]
    [string] $TestName = "All",

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistry = "edgebuilds.azurecr.io",

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistryUsername = "EdgeBuilds",

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistryPassword = $(Throw "Container registry password is required"),

    [ValidateNotNullOrEmpty()]
    [string] $IoTHubConnectionString = $(Throw "IoT hub connection string is required"),

    [ValidateNotNullOrEmpty()]
    [string] $EventHubConnectionString = $(Throw "Event hub connection string is required"),

    [ValidateNotNullOrEmpty()]
    [string] $EdgeE2ERootCACertRSAFile = $null,

    [ValidateNotNullOrEmpty()]
    [string] $EdgeE2ERootCAKeyRSAFile = $null,

    [ValidateNotNullOrEmpty()]
    [string] $EdgeE2ETestRootCAPassword = $null,

    [ValidateScript({($_ -as [System.Uri]).AbsoluteUri -ne $null})]
    [string] $ProxyUri = $null
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"
$global:ProgressPreference = "SilentlyContinue"

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
        ($TestName -eq "TempFilterFunctions") -Or
        (($ProxyUri) -and ($TestName -in "TempSensor", "QuickstartCerts", "TransparentGateway")))
    {
        Switch -Regex ($TestName)
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
            "TempSensor" # Only when $ProxyUri is specified
            {
                Write-Host "Copy deployment file from $QuickstartDeploymentArtifactFilePath"
                Copy-Item $QuickstartDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
            "QuickstartCerts|TransparentGateway" # Only when $ProxyUri is specified
            {
                Write-Host "Copy deployment file from $RuntimeOnlyDeploymentArtifactFilePath"
                Copy-Item $RuntimeOnlyDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
        }

        $ImageArchitectureLabel = $(GetImageArchitectureLabel)
        (Get-Content $DeploymentWorkingFilePath).replace('<Architecture>', $ImageArchitectureLabel) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<OptimizeForPerformance>', 'true') | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<Build.BuildNumber>', $ArtifactImageBuildNumber) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<CR.Username>', $ContainerRegistryUsername) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<CR.Password>', $ContainerRegistryPassword) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('-linux-', '-windows-') | Set-Content $DeploymentWorkingFilePath

        If ($ProxyUri)
        {
            # Add/remove/edit JSON values *after* replacing all the '<>' placeholders because
            # ConvertTo-Json will encode angle brackets.
            $httpsProxy = "{ `"value`": `"$ProxyUri`" }" | ConvertFrom-Json
            $json = Get-Content $DeploymentWorkingFilePath | ConvertFrom-Json
            $edgeAgentDesired = $json.modulesContent.'$edgeAgent'.'properties.desired'
            $upstreamProtocol = $edgeAgentDesired.systemModules.edgeHub.env.PSObject.Properties['UpstreamProtocol']
            If (($upstreamProtocol -ne $null) -and ($upstreamProtocol.Value.value -eq 'Mqtt')) {
                $upstreamProtocol = '{ "value": "MqttWs" }' | ConvertFrom-Json
            } else {
                $upstreamProtocol = '{ "value": "AmqpWs" }' | ConvertFrom-Json
            }

            # Add edgeAgent env with 'https_proxy' and 'UpstreamProtocol'
            if ($edgeAgentDesired.systemModules.edgeAgent.PSObject.Properties['env'] -eq $null) {
                $edgeAgentDesired.systemModules.edgeAgent | `
                    Add-Member -Name 'env' -Value ([pscustomobject]@{}) -MemberType NoteProperty
            }
            $edgeAgentDesired.systemModules.edgeAgent.env | `
                Add-Member -Name 'https_proxy' -Value $httpsProxy -MemberType NoteProperty
            $edgeAgentDesired.systemModules.edgeAgent.env | `
                Add-Member -Name 'UpstreamProtocol' -Value $upstreamProtocol -MemberType NoteProperty -Force

            # Add 'https_proxy' and 'UpstreamProtocol' to edgeHub env
            $edgeAgentDesired.systemModules.edgeHub.env | `
                Add-Member -Name 'https_proxy' -Value $httpsProxy -MemberType NoteProperty
            $edgeAgentDesired.systemModules.edgeHub.env | `
                Add-Member -Name 'UpstreamProtocol' -Value $upstreamProtocol -MemberType NoteProperty -Force

            $json | ConvertTo-Json -Depth 20 | Set-Content $DeploymentWorkingFilePath
        }
    }
}

Function PrepareCertificateTools
{
    # setup environment before invoking cert gen script
    $OpenSSLExeName="openssl.exe"
    if ($null -eq (Get-Command $OpenSSLExeName -ErrorAction SilentlyContinue))
    {
        # if openssl is not in path add default openssl install path and try again
        $env:PATH += ";$DefaultOpensslInstallPath"
        if ($null -eq (Get-Command $OpenSSLExeName -ErrorAction SilentlyContinue))
        {
            throw ("$OpenSSLExeName is unavailable. Please install $OpenSSLExeName and set it in the PATH before proceeding.")
        }
    }
    $env:FORCE_NO_PROD_WARNING="True"
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
        Invoke-Expression "$dockerCmd logs edgeAgent" | Out-Host
    }
    Catch
    {
        Write-Host "Exception caught when output Edge Agent logs"
    }

    Try
    {
        Write-Host "EDGE HUB LOGS"
        Invoke-Expression "$dockerCmd logs edgeHub" | Out-Host
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
            Invoke-Expression "$dockerCmd logs tempSensor" | Out-Host
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
            Invoke-Expression "$dockerCmd logs tempFilter" | Out-Host
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
            Invoke-Expression "$dockerCmd logs tempFilterFunctions" | Out-Host
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
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-DMAmqp"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
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
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-DMMqtt"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            --upstream-protocol 'MqttWs' ``
            --proxy `"$ProxyUri`""
    }
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
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-QuickstartCerts"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"doesNotNeed`" ``
        -n `"$env:computername`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" ``
        -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        --optimize_for_performance true ``
        --leave-running=All ``
        --no-verify"
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            -l `"$DeploymentWorkingFilePath`" ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host

    $caCertPath = (Get-ChildItem C:\ProgramData\iotedge\hsm\certs\edge_owner_ca*.pem | Select -First 1).FullName
    Write-Host "CA certificate path=$caCertPath"

    Write-Host "Run LeafDevice"
    $testCommand = "&$LeafDeviceExeTestPath ``
        -d `"${deviceId}-leaf`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"$EventHubConnectionString`" ``
        -ct `"$caCertPath`" ``
        -ed `"$env:computername`""
    If ($ProxyUri) {
        $testCommand = "$testCommand --proxy `"$ProxyUri`""
    }
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunTempFilterTest
{
    PrintHighlightedMessage "Run TempFilter test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-tempFilter"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilter`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
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
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-tempFilterFunc"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilterFunctions`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`""
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
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
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-tempSensor"
    PrintHighlightedMessage "Run quickstart test with -d ""$deviceId"" started at $testStartAt."

    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"$EventHubConnectionString`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" ``
        -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        -tw `"$TwinTestFileArtifactFilePath`" ``
        --optimize_for_performance true"
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            -l `"$DeploymentWorkingFilePath`" ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunLeafDeviceTest
(
    [ValidateSet("sas","x509CA","x509Thumprint")][string]$authType,
    [ValidateSet("Mqtt","MqttWs","Amqp", "AmqpWs")][string]$protocol,
    [ValidateNotNullOrEmpty()][string]$leafDeviceId,
    [string]$edgeDeviceId
)
{
    $testCommand = $null
    switch ($authType) {
        "sas"
        {
            if ([string]::IsNullOrWhiteSpace($edgeDeviceId))
            {
                Write-Host "Run LeafDevice with SAS auth not in scope"
                $testCommand = "&$LeafDeviceExeTestPath ``
                    -d `"$leafDeviceId`" ``
                    -c `"$IoTHubConnectionString`" ``
                    -e `"$EventHubConnectionString`" ``
                    -proto `"$protocol`" ``
                    -ct `"$TrustedCACertificatePath`" ``
                    -ed `"$env:computername`""
            }
            else
            {
                Write-Host "Run LeafDevice with SAS auth in scope"
                $testCommand = "&$LeafDeviceExeTestPath ``
                    -d `"$leafDeviceId`" ``
                    -c `"$IoTHubConnectionString`" ``
                    -e `"$EventHubConnectionString`" ``
                    -proto `"$protocol`" ``
                    -ct `"$TrustedCACertificatePath`" ``
                    -ed-id `"$edgeDeviceId`" ``
                    -ed `"$env:computername`""
            }
            break
        }

        "x509CA"
        {
            if ([string]::IsNullOrWhiteSpace($edgeDeviceId))
            {
                $(Throw "For X.509 leaf device, the Edge device Id is requried")
            }
            Write-Host "Run LeafDevice with X.509 CA auth in scope"
            New-CACertsDevice "$leafDeviceId"
            $testCommand = "&$LeafDeviceExeTestPath ``
                -d `"$leafDeviceId`" ``
                -c `"$IoTHubConnectionString`" ``
                -e `"$EventHubConnectionString`" ``
                -proto `"$protocol`" ``
                -ct `"$TrustedCACertificatePath`" ``
                -cac `"$EdgeCertGenScriptDir\certs\iot-device-${leafDeviceId}-full-chain.cert.pem`" ``
                -cak `"$EdgeCertGenScriptDir\private\iot-device-${leafDeviceId}.key.pem`" ``
                -ed-id `"$edgeDeviceId`" ``
                -ed `"$env:computername`""
            break
        }

        "x509Thumprint"
        {
            if ([string]::IsNullOrWhiteSpace($edgeDeviceId))
            {
                $(Throw "For X.509 leaf device, the Edge device Id is requried")
            }
            Write-Host "Run LeafDevice with X.509 thumbprint auth in scope"
            New-CACertsDevice "$leafDeviceId-pri"
            New-CACertsDevice "$leafDeviceId-sec"
            $testCommand = "&$LeafDeviceExeTestPath ``
                -d `"$leafDeviceId`" ``
                -c `"$IoTHubConnectionString`" ``
                -e `"$EventHubConnectionString`" ``
                -proto `"$protocol`" ``
                -ct `"$TrustedCACertificatePath`" ``
                -ctpc `"$EdgeCertGenScriptDir\certs\iot-device-${leafDeviceId}-pri-full-chain.cert.pem`" ``
                -ctpk `"$EdgeCertGenScriptDir\private\iot-device-${leafDeviceId}-pri.key.pem`" ``
                -ctsc `"$EdgeCertGenScriptDir\certs\iot-device-${leafDeviceId}-sec-full-chain.cert.pem`" ``
                -ctsk `"$EdgeCertGenScriptDir\private\iot-device-${leafDeviceId}-sec.key.pem`" ``
                -ed-id `"$edgeDeviceId`" ``
                -ed `"$env:computername`""
            break
        }

        default
        {
            $(Throw "Unsupported auth mode $authType")
        }
     }

    If ($ProxyUri) {
        $testCommand = "$testCommand --proxy `"$ProxyUri`""
    }

    $testStartAt = Get-Date
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode
    PrintLogs $testStartAt $testExitCode

    Return $testExitCode
}

Function RunTransparentGatewayTest
{
    PrintHighlightedMessage "Run Transparent Gateway test for $Architecture"

    if ([string]::IsNullOrWhiteSpace($EdgeE2ERootCACertRSAFile))
    {
        $EdgeE2ERootCACertRSAFile=$DefaultInstalledRSARootCACert
    }
    if ([string]::IsNullOrWhiteSpace($EdgeE2ERootCAKeyRSAFile))
    {
        $EdgeE2ERootCAKeyRSAFile=$DefaultInstalledRSARootCAKey
    }
    TestSetup

    $testStartAt = Get-Date
    $edgeDeviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-TransGW"
    PrintHighlightedMessage "Run quickstart test with -d ""$edgeDeviceId"" started at $testStartAt."

    # setup certificate generation tools to create the Edge device and leaf device certificates
    PrepareCertificateTools
    # dot source the certificate generation script
    . "$EdgeCertGenScript"
    # install the provided root CA to seed the certificate chain
    Install-RootCACertificate $EdgeE2ERootCACertRSAFile $EdgeE2ERootCAKeyRSAFile "rsa" $EdgeE2ETestRootCAPassword

    # generate the edge gateway certs
    New-CACertsEdgeDevice $edgeDeviceId

    #launch the edge as a transparent gateway
    $testCommand = "&$IoTEdgeQuickstartExeTestPath ``
        -d `"$edgeDeviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"doesNotNeed`" ``
        -n `"$env:computername`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" ``
        -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        --device_ca_cert `"$EdgeCertGenScriptDir\certs\iot-edge-device-$edgeDeviceId-full-chain.cert.pem`" ``
        --device_ca_pk `"$EdgeCertGenScriptDir\private\iot-edge-device-$edgeDeviceId.key.pem`" ``
        --trusted_ca_certs `"$TrustedCACertificatePath`" ``
        --optimize_for_performance true ``
        --leave-running=All ``
        --no-verify"

    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            -l `"$DeploymentWorkingFilePath`" ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host

    # run the various leaf device tests
    $deviceId = "e2e-${ReleaseLabel}-Win-${Architecture}"
    RunLeafDeviceTest "sas" "Mqtt" "$deviceId-mqtt-sas-noscope-leaf" $null
    RunLeafDeviceTest "sas" "Amqp" "$deviceId-amqp-sas-noscope-leaf" $null

    RunLeafDeviceTest "sas" "Mqtt" "$deviceId-mqtt-sas-inscope-leaf" $edgeDeviceId
    RunLeafDeviceTest "sas" "Amqp" "$deviceId-amqp-sas-inscope-leaf" $edgeDeviceId

    RunLeafDeviceTest "x509CA" "Mqtt" "$deviceId-mqtt-x509ca-inscope-leaf" $edgeDeviceId
    RunLeafDeviceTest "x509CA" "Amqp" "$deviceId-amqp-x509ca-inscope-leaf" $edgeDeviceId

    RunLeafDeviceTest "x509Thumprint" "Mqtt" "$deviceId-mqtt-x509th-inscope-leaf" $edgeDeviceId
    RunLeafDeviceTest "x509Thumprint" "Amqp" "$deviceId-amqp-x509th-inscope-leaf" $edgeDeviceId

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

    Return $testExitCode
}

Function TestSetup
{
    ValidateTestParameters
    CleanUp | Out-Host
    InitializeWorkingFolder
    PrepareTestFromArtifacts
}

Function ValidateTestParameters
{
    PrintHighlightedMessage "Validate test parameters for $TestName"

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
        if ($ProxyUri)
        {
            $validatingItems += $RuntimeOnlyDeploymentArtifactFilePath
        }
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
        if ($ProxyUri)
        {
            $validatingItems += $QuickstartDeploymentArtifactFilePath
        }
        $validatingItems += $TwinTestFileArtifactFilePath
    }

    If ($TestName -eq "TransparentGateway")
    {
        $validatingItems += $EdgeCertGenScriptDir
        $validatingItems += $EdgeE2ERootCACertRSAFile
        $validatingItems += $EdgeE2ERootCAKeyRSAFile
    }

    $validatingItems | ForEach-Object {
        If (-Not (Test-Path -Path $_))
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
$E2ETestFolder = (Resolve-Path $E2ETestFolder).Path
$DefaultOpensslInstallPath = "C:\vcpkg\installed\x64-windows\tools\openssl"
$InstallationScriptPath = Join-Path $E2ETestFolder "artifacts\core-windows\scripts\windows\setup\IotEdgeSecurityDaemon.ps1"
$EdgeCertGenScriptDir = Join-Path $E2ETestFolder "artifacts\core-windows\CACertificates"
$EdgeCertGenScript = Join-Path $EdgeCertGenScriptDir "ca-certs.ps1"
$DefaultInstalledRSARootCACert = Join-Path $EdgeCertGenScriptDir "rsa_root_ca.cert.pem"
$DefaultInstalledRSARootCAKey = Join-Path $EdgeCertGenScriptDir "rsa_root_ca.key.pem"
$TrustedCACertificatePath= Join-Path $EdgeCertGenScriptDir "\certs\azure-iot-test-only.root.ca.cert.pem"
$ModuleToModuleDeploymentFilename = "module_to_module_deployment.template.json"
$ModuleToFunctionsDeploymentFilename = "module_to_functions_deployment.template.json"
$DirectMethodModuleToModuleDeploymentFilename = "dm_module_to_module_deployment.json"
$RuntimeOnlyDeploymentFilename = 'runtime_only_deployment.template.json'
$QuickstartDeploymentFilename = 'quickstart_deployment.template.json'
$TwinTestFilename = "twin_test_tempSensor.json"

$IoTEdgeQuickstartArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\IoTEdgeQuickstart\$Architecture"
$LeafDeviceArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\LeafDevice\$Architecture"
$IoTEdgedArtifactFolder = Join-Path $E2ETestFolder "artifacts\iotedged-windows"
$PackagesArtifactFolder = Join-Path $E2ETestFolder "artifacts\packages"
$DeploymentFilesFolder = Join-Path $E2ETestFolder "artifacts\core-windows\e2e_deployment_files"
$TestFileFolder = Join-Path $E2ETestFolder "artifacts\core-windows\e2e_test_files"
$ModuleToModuleDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $ModuleToModuleDeploymentFilename
$ModuleToFunctionDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $ModuleToFunctionsDeploymentFilename
$RuntimeOnlyDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $RuntimeOnlyDeploymentFilename
$QuickstartDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $QuickstartDeploymentFilename
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

&$InstallationScriptPath

$retCode = RunTest
Write-Host "Exit test with code $retCode"
Exit $retCode -gt 0