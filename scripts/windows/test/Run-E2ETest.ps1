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
            "All", "DirectMethodAmqp", "DirectMethodAmqpMqtt", "DirectMethodMqtt", "DirectMethodMqttAmqp", "LongHaul", "QuickstartCerts", "Stress", "TempFilter", "TempFilterFunctions", "TempSensor", "TransparentGateway"

    .PARAMETER ContainerRegistry
        Host address of container registry. It could be azure container registry, docker hub, or your own hosted container registry.

    .PARAMETER ContainerRegistryUsername
        Username of container registry.

    .PARAMETER ContainerRegistryPassword
        Password of given username for container registory

    .PARAMETER DesiredModulesToRestartCSV
        Optional CSV string of module names for long haul specifying what modules to restart. If specified, then "RestartIntervalInMins" must be specified as well.

    .PARAMETER IoTHubConnectionString
        IoT hub connection string for creating edge device

    .PARAMETER EventHubConnectionString
        Event hub connection string for receive D2C messages

    .PARAMETER EventHubConsumerGroupId
        Event hub consumer group id used by the Analyzer module to subscribe to events from the Event Hub endpoint.

    .PARAMETER ProxyUri
        (Optional) The URI of an HTTPS proxy server; if specified, all communications to IoT Hub will go through this proxy.

    .PARAMETER LoadGenMessageFrequency
        Frequency to send messages in LoadGen module for long haul and stress test. Default is 00.00.01 for long haul and 00:00:00.03 for stress test.

    .PARAMETER RestartIntervalInMins
        Optional value for long haul specifying how often a random module will restart. If specified, then "desiredModulesToRestartJsonPath" must be specified as well.

    .PARAMETER SnitchAlertUrl
        Alert Url pointing to Azure Logic App for email preparation and sending for long haul and stress test.

    .PARAMETER SnitchBuildNumber
        Build number for snitcher docker image for long haul and stress test. Default is 1.1.

    .PARAMETER SnitchReportingIntervalInSecs
        Reporting frequency in seconds to send status email for long hual and stress test. Default is 86400 (1 day) for long haul and 1700000 for stress test.

    .PARAMETER SnitchStorageAccount
        Azure blob Sstorage account for store logs used in status email for long haul and stress test.

    .PARAMETER SnitchStorageMasterKey
        Master key of snitch storage account for long haul and stress test.

    .PARAMETER SnitchTestDurationInSecs
        Test duration in seconds for long haul and stress test.

    .PARAMETER TransportType1
        Transport type for LoadGen1 and TwinTester1 for stress test. Default is amqp.

    .PARAMETER TransportType2
        Transport type for LoadGen2 and TwinTester2 for stress test. Default is amqp.

    .PARAMETER TransportType3
        Transport type for LoadGen3 and TwinTester3 for stress test. Default is mqtt.

    .PARAMETER TransportType4
        Transport type for LoadGen4 and TwinTester4 for stress test. Default is mqtt.

    .PARAMETER AmqpSettingsEnabled
        Enable amqp protocol head in Edge Hub.

    .PARAMETER MqttSettingsEnabled
        Enable mqtt protocol head in Edge Hub.

    .PARAMETER DpsScopeId
        DPS scope id. Required only when using DPS to provision the device.

    .PARAMETER DpsMasterSymmetricKey
        DPS master symmetric key. Required only when using DPS symmetric key to provision the Edge device.
    
    .PARAMETER LogAnalyticsEnabled
        Optional Log Analytics enable string for the Analyzer module. If LogAnalyticsEnabled is set to enable (true), the rest of Log Analytics parameters must be provided.

    .PARAMETER LogAnalyticsWorkspaceId
        Optional Log Analytics workspace ID for metrics collection and reporting.

    .PARAMETER LogAnalyticsSharedKey
        Optional Log Analytics shared key for metrics collection and reporting.

    .PARAMETER LogAnalyticsLogType
        Optional Log Analytics log type for the Analyzer module.

    .PARAMETER TwinUpdateSize
        Specifies the char count (i.e. size) of each twin update. Default is 1 for long haul and 100 for stress test.

    .PARAMETER TwinUpdateFrequency
        Frequency to make twin updates. This should be specified in DateTime format. Default is 00:00:15 for long haul and 00:00:05 for stress test.

    .PARAMETER TwinUpdateFailureThreshold
        Specifies the longest period of time a twin update can take before being marked as a failure. This should be specified in DateTime format. Default is 00:01:00

    .PARAMETER MetricsEndpointsCSV
        Optional CSV of exposed endpoints for which to scrape metrics.

    .PARAMETER MetricsScrapeFrequencyInSecs
        Optional frequency at which the MetricsCollector module will scrape metrics from the exposed metrics endpoints. Default is 300 seconds. 

    .PARAMETER MetricsUploadTarget
        Optional upload target for metrics. Valid values are AzureLogAnalytics or IoTHub. Default is AzureLogAnalytics. 

    .EXAMPLE
        .\Run-E2ETest.ps1
            -E2ETestFolder "C:\Data\e2etests"
            -ReleaseLabel "Release-ARM-1"
            -ArtifactImageBuildNumber "20190101.1"
            -TestName "TempSensor"
            -ContainerRegistry "yourpipeline.azurecr.io"
            -ContainerRegistryUsername "xxxx"
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
            -ContainerRegistry "yourpipeline.azurecr.io"
            -ContainerRegistryUsername "xxxx"
            -ContainerRegistryPassword "xxxx"
            -IoTHubConnectionString "xxxx"
            -EventHubConnectionString "xxxx"
            -ProxyUri "http://proxyserver:3128"
            -EdgeE2ERootCACertRSAFile "file path"  #if not provided, a default path will be checked
            -EdgeE2ERootCAKeyRSAFile "file path"   #if not provided, a default path will be checked
            -EdgeE2ETestRootCAPassword "xxxx"

        DPS symmetric key provisioning test command
        .\Run-E2ETest.ps1
            -E2ETestFolder "C:\Data\e2etests"
            -ReleaseLabel "Release-ARM-1"
            -ArtifactImageBuildNumber "20190101.1"
            -TestName "DpsSymmetricKeyProvisioning"
            -ContainerRegistry "yourpipeline.azurecr.io"
            -ContainerRegistryUsername "xxxx"
            -ContainerRegistryPassword "xxxx"
            -IoTHubConnectionString "xxxx"
            -EventHubConnectionString "xxxx"
            -ProxyUri "http://proxyserver:3128"
            -DpsScopeId "scope-id"
            -DpsMasterSymmetricKey "key"

        DPS X.509 provisioning test command using test generated identity certificates
        .\Run-E2ETest.ps1
            -E2ETestFolder "C:\Data\e2etests"
            -ReleaseLabel "Release-ARM-1"
            -ArtifactImageBuildNumber "20190101.1"
            -TestName "DpsX509Provisioning"
            -ContainerRegistry "yourpipeline.azurecr.io"
            -ContainerRegistryUsername "xxxx"
            -ContainerRegistryPassword "xxxx"
            -IoTHubConnectionString "xxxx"
            -EventHubConnectionString "xxxx"
            -ProxyUri "http://proxyserver:3128"
            -DpsScopeId "scope-id"
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

    [ValidateSet("All",
                 "DirectMethodAmqp",
                 "DirectMethodAmqpMqtt",
                 "DirectMethodMqtt",
                 "DirectMethodMqttAmqp",
                 "DpsSymmetricKeyProvisioning",
                 "DpsTpmProvisioning",
                 "DpsX509Provisioning",
                 "LongHaul",
                 "QuickstartCerts",
                 "Stress",
                 "TempFilter",
                 "TempFilterFunctions",
                 "TempSensor",
                 "TransparentGateway")]
    [string] $TestName = "All",

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistry = "edgebuilds.azurecr.io",

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistryUsername = $(Throw "Container registry username is required"),

    [ValidateNotNullOrEmpty()]
    [string] $ContainerRegistryPassword = $(Throw "Container registry password is required"),

    [string] $DesiredModulesToRestartCSV = $null,

    [ValidateNotNullOrEmpty()]
    [string] $DpsScopeId = $null,

    [ValidateNotNullOrEmpty()]
    [string] $DpsMasterSymmetricKey = $null,

    [ValidateNotNullOrEmpty()]
    [string] $EventHubConnectionString = $(Throw "Event hub connection string is required"),

    [string] $EventHubConsumerGroupId = '$Default',

    [string] $EdgeE2ERootCACertRSAFile = $null,

    [string] $EdgeE2ERootCAKeyRSAFile = $null,

    [ValidateNotNullOrEmpty()]
    [string] $EdgeE2ETestRootCAPassword = $null,

    [ValidateNotNullOrEmpty()]
    [string] $IoTHubConnectionString = $(Throw "IoT hub connection string is required"),

    [string] $LoadGenMessageFrequency = $null,

    [string] $MetricsEndpointsCSV = $null,

    [string] $MetricsScrapeFrequencyInSecs = $null,

    [string] $MetricsUploadTarget = $null,

    [ValidateScript({($_ -as [System.Uri]).AbsoluteUri -ne $null})]
    [string] $ProxyUri = $null,

    [string] $RestartIntervalInMins = $null,

    [ValidateNotNullOrEmpty()]
    [string] $SnitchAlertUrl = $null,

    [string] $SnitchBuildNumber = "1.2",

    [string] $SnitchReportingIntervalInSecs = $null,

    [ValidateNotNullOrEmpty()]
    [string] $SnitchStorageAccount = $null,

    [ValidateNotNullOrEmpty()]
    [string] $SnitchStorageMasterKey = $null,

    [string] $SnitchTestDurationInSecs = $null,

    [string] $TransportType1 = "amqp",

    [string] $TransportType2 = "amqp",

    [string] $TransportType3 = "mqtt",

    [string] $TransportType4 = "mqtt",

    [ValidateSet("true", "false")]
    [string] $AmqpSettingsEnabled = "true",

    [ValidateSet("true", "false")]
    [string] $MqttSettingsEnabled = "true",

    [ValidateSet("true", "false")]
    [string] $LogAnalyticsEnabled = "false",

    [string] $LogAnalyticsWorkspaceId = $null,

    [string] $LogAnalyticsSharedKey = $null,

    [string] $LogAnalyticsLogType = $null,

    [string] $TwinUpdateSize = $null,

    [string] $TwinUpdateFrequency = $null,

    [string] $TwinUpdateFailureThreshold = $null,

    [switch] $BypassEdgeInstallation
)

Add-Type -TypeDefinition @"
public enum DpsProvisioningType
{
    SymmetricKey = 0,
    Tpm = 1,
    X509 = 2,
}
"@

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

Function GetLongHaulDeploymentFilename
{
    If (GetImageArchitectureLabel -eq "amd64")
    {
        # Using versions without snitcher and influxdb, as they are currently not working in windows
        return "long_haul_deployment.template.windows.json"
    }
    Throw "Unsupported long haul test architecture: $Architecture"
}

Function GetStressDeploymentFilename
{
    If (GetImageArchitectureLabel -eq "amd64")
    {
        # Using versions without snitcher and influxdb, as they are currently not working in windows
        return "stress_deployment.template.windows.json"
    }
    Throw "Unsupported stress test architecture: $Architecture"
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
    Write-Host "Copy IoT Edge Quickstart from $IotEdgeQuickstartArtifactFolder to $QuickstartWorkingFolder"
    Copy-Item $IotEdgeQuickstartArtifactFolder -Destination $QuickstartWorkingFolder -Recurse -Force

    # Leaf device
    If (($TestName -eq "QuickstartCerts") -Or ($TestName -eq "TransparentGateway"))
    {
        Write-Host "Copy Leaf device from $LeafDeviceArtifactFolder to $LeafDeviceWorkingFolder"
        Copy-Item $LeafDeviceArtifactFolder -Destination $LeafDeviceWorkingFolder -Recurse -Force
    }

    # Deployment file
    If (($TestName -like "DirectMethod*") -Or
        ($TestName -like "Dps*") -Or
        ($TestName -eq "LongHaul") -Or
        ($TestName -eq "Stress") -Or
        ($TestName -eq "TempFilter") -Or
        ($TestName -eq "TempFilterFunctions") -Or
        (($ProxyUri) -and ($TestName -in "TempSensor", "QuickstartCerts", "TransparentGateway")))
    {
        Switch -regex ($TestName)
        {
            "DirectMethod.*"
            {
                Write-Host "Copy deployment file from $DirectMethodModuleToModuleDeploymentArtifactFilePath"
                Copy-Item $DirectMethodModuleToModuleDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force

                Switch ($TestName)
                {
                    "DirectMethodAmqp"
                    {
                        (Get-Content $DeploymentWorkingFilePath).replace('<UpstreamProtocol>','Amqp') | Set-Content $DeploymentWorkingFilePath
                        (Get-Content $DeploymentWorkingFilePath).replace('<ClientTransportType>','Amqp_Tcp_Only') | Set-Content $DeploymentWorkingFilePath
                    }
                    "DirectMethodAmqpMqtt"
                    {
                        (Get-Content $DeploymentWorkingFilePath).replace('<UpstreamProtocol>','Amqp') | Set-Content $DeploymentWorkingFilePath
                        (Get-Content $DeploymentWorkingFilePath).replace('<ClientTransportType>','Mqtt_Tcp_Only') | Set-Content $DeploymentWorkingFilePath
                    }
                    "DirectMethodMqtt"
                    {
                        (Get-Content $DeploymentWorkingFilePath).replace('<UpstreamProtocol>','Mqtt') | Set-Content $DeploymentWorkingFilePath
                        (Get-Content $DeploymentWorkingFilePath).replace('<ClientTransportType>','Mqtt_Tcp_Only') | Set-Content $DeploymentWorkingFilePath
                    }
                    "DirectMethodMqttAmqp"
                    {
                        (Get-Content $DeploymentWorkingFilePath).replace('<UpstreamProtocol>','Mqtt') | Set-Content $DeploymentWorkingFilePath
                        (Get-Content $DeploymentWorkingFilePath).replace('<ClientTransportType>','Amqp_Tcp_Only') | Set-Content $DeploymentWorkingFilePath
                    }
                }
            }
            "Dps.*"
            {
                Write-Host "Copy deployment file from $QuickstartDeploymentArtifactFilePath"
                Copy-Item $QuickstartDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
            {$_ -in "LongHaul","Stress"}
            {
                If ($TestName -eq "LongHaul")
                {
                    Write-Host "Copy deployment file from $LongHaulDeploymentArtifactFilePath"
                    Copy-Item $LongHaulDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
                    (Get-Content $DeploymentWorkingFilePath).replace('<DesiredModulesToRestartCSV>',$DesiredModulesToRestartCSV) | Set-Content $DeploymentWorkingFilePath
                    (Get-Content $DeploymentWorkingFilePath).replace('<RestartIntervalInMins>',$RestartIntervalInMins) | Set-Content $DeploymentWorkingFilePath
                }
                Else
                {
                    Write-Host "Copy deployment file from $StressDeploymentArtifactFilePath"
                    Copy-Item $StressDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
                    (Get-Content $DeploymentWorkingFilePath).replace('<TransportType1>',$TransportType1) | Set-Content $DeploymentWorkingFilePath
                    (Get-Content $DeploymentWorkingFilePath).replace('<TransportType2>',$TransportType2) | Set-Content $DeploymentWorkingFilePath
                    (Get-Content $DeploymentWorkingFilePath).replace('<TransportType3>',$TransportType3) | Set-Content $DeploymentWorkingFilePath
                    (Get-Content $DeploymentWorkingFilePath).replace('<TransportType4>',$TransportType4) | Set-Content $DeploymentWorkingFilePath
                    (Get-Content $DeploymentWorkingFilePath).replace('<amqpSettings__enabled>',$AmqpSettingsEnabled) | Set-Content $DeploymentWorkingFilePath
                    (Get-Content $DeploymentWorkingFilePath).replace('<mqttSettings__enabled>',$MqttSettingsEnabled) | Set-Content $DeploymentWorkingFilePath
                }

                (Get-Content $DeploymentWorkingFilePath).replace('<Analyzer.ConsumerGroupId>',$EventHubConsumerGroupId) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Analyzer.EventHubConnectionString>',$EventHubConnectionString) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Analyzer.LogAnalyticsEnabled>',$LogAnalyticsEnabled) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Analyzer.LogAnalyticsLogType>',$LogAnalyticsLogType) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<LogAnalyticsWorkspaceId>',$LogAnalyticsWorkspaceId) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<LogAnalyticsSharedKey>',$LogAnalyticsSharedKey) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<LoadGen.MessageFrequency>',$LoadGenMessageFrequency) | Set-Content $DeploymentWorkingFilePath
                $escapedBuildId= $ArtifactImageBuildNumber -replace "\.",""
                (Get-Content $DeploymentWorkingFilePath).replace('<ServiceClientConnectionString>',$IoTHubConnectionString) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<MetricsCollector.MetricsEndpointsCSV>',$MetricsEndpointsCSV) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<MetricsCollector.ScrapeFrequencyInSecs>',$MetricsScrapeFrequencyInSecs) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<MetricsCollector.UploadTarget>',$MetricsUploadTarget) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.AlertUrl>',$SnitchAlertUrl) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.BuildNumber>',$SnitchBuildNumber) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.BuildId>',"$ReleaseLabel-$(GetImageArchitectureLabel)-windows-$escapedBuildId") | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.ReportingIntervalInSecs>',$SnitchReportingIntervalInSecs) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.StorageAccount>',$SnitchStorageAccount) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.StorageMasterKey>',$SnitchStorageMasterKey) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.TestDurationInSecs>',$SnitchTestDurationInSecs) | Set-Content $DeploymentWorkingFilePath
                $SnitcherBinds = "\""$($env:ProgramData.Replace("\", "\\\\"))\\\\iotedge\\\\mgmt:$($env:ProgramData.Replace("\", "\\\\"))\\\\iotedge\\\\mgmt\"""
                (Get-Content $DeploymentWorkingFilePath).replace('<Snitch.Binds>',$SnitcherBinds) | Set-Content $DeploymentWorkingFilePath
                $ManagementUri = "unix:///$($env:ProgramData.Replace("\", "/"))/iotedge/mgmt/sock"
                (Get-Content $DeploymentWorkingFilePath).replace('<Management.Uri>',$ManagementUri) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<TwinUpdateSize>',$TwinUpdateSize) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<TwinUpdateFrequency>',$TwinUpdateFrequency) | Set-Content $DeploymentWorkingFilePath
                (Get-Content $DeploymentWorkingFilePath).replace('<TwinUpdateFailureThreshold>',$TwinUpdateFailureThreshold) | Set-Content $DeploymentWorkingFilePath
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
            "TempSensor"
            {
                Write-Host "Copy deployment file from $QuickstartDeploymentArtifactFilePath"
                Copy-Item $QuickstartDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
            {$_ -in "QuickstartCerts","TransparentGateway"} # Only when $ProxyUri is specified
            {
                Write-Host "Copy deployment file from $RuntimeOnlyDeploymentArtifactFilePath"
                Copy-Item $RuntimeOnlyDeploymentArtifactFilePath -Destination $DeploymentWorkingFilePath -Force
            }
        }

        $ImageArchitectureLabel = $(GetImageArchitectureLabel)
        (Get-Content $DeploymentWorkingFilePath).replace('<Architecture>', $ImageArchitectureLabel) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<Build.BuildNumber>', $ArtifactImageBuildNumber) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<CR.Username>', $ContainerRegistryUsername) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<CR.Password>', $ContainerRegistryPassword) | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('-linux-', '-windows-') | Set-Content $DeploymentWorkingFilePath
        (Get-Content $DeploymentWorkingFilePath).replace('<Container_Registry>', $ContainerRegistry) | Set-Content $DeploymentWorkingFilePath

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

    $TestName = "DirectMethodAmqpMqtt"
    $testExitCode = RunDirectMethodAmqpMqttTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "DirectMethodMqtt"
    $testExitCode = RunDirectMethodMqttTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "DirectMethodMqttAmqp"
    $testExitCode = RunDirectMethodMqttAmqpTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "DpsSymmetricKeyProvisioning"
    $testExitCode = RunDpsProvisioningTest ([DpsProvisioningType]::SymmetricKey)
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "DpsTpmProvisioning"
    $testExitCode = RunDpsProvisioningTest ([DpsProvisioningType]::Tpm)
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "DpsX509Provisioning"
    $testExitCode = RunDpsProvisioningTest ([DpsProvisioningType]::X509)
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "QuickstartCerts"
    $testExitCode = RunQuickstartCertsTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "TempFilter"
    $testExitCode = RunTempFilterTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "TempFilterFunctions"
    $testExitCode = RunTempFilterFunctionsTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "TempSensor"
    $testExitCode = RunTempSensorTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    $TestName = "TransparentGateway"
    $testExitCode = RunTransparentGatewayTest
    $lastTestExitCode = If ($testExitCode -ne 0) { $testExitCode } Else { $lastTestExitCode }

    Return $lastTestExitCode
}

Function RunDirectMethodAmqpTest
{
    PrintHighlightedMessage "Run Direct Method test with Amqp/AmqpWs upstream protocol and Amqp client transport type for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-DMAmqp"
    PrintHighlightedMessage "Run direct method test with Amqp/AmqpWs upstream protocol and Amqp client transport type on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            $BypassInstallationFlag"
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

Function RunDirectMethodAmqpMqttTest
{
    PrintHighlightedMessage "Run Direct Method test with Amqp/AmqpWs upstream protocol and Mqtt client transport type for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-DMAmqpMqtt"
    PrintHighlightedMessage "Run direct method test with Amqp/AmqpWs upstream protocol and Mqtt client transport type on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            $BypassInstallationFlag"
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
    PrintHighlightedMessage "Run Direct Method test with Mqtt/MqttWs upstream protocol and Mqtt client transport type for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-DMMqtt"
    PrintHighlightedMessage "Run direct method test with Mqtt/MqttWs upstream protocol and Mqtt client transport type on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            $BypassInstallationFlag"
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

Function RunDirectMethodMqttAmqpTest
{
    PrintHighlightedMessage "Run Direct Method test with Mqtt/MqttWs upstream protocol and Amqp client transport type for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-DMMqttAmqp"
    PrintHighlightedMessage "Run direct method test with Mqtt/MqttWs upstream protocol and Amqp client transport type on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"DirectMethodSender`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            $BypassInstallationFlag"
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

Function RunDpsProvisioningTest
{
    param
    (
        [DpsProvisioningType] $provisioningType
    )

    PrintHighlightedMessage "Run DPS provisioning test $provisioningType"
    TestSetup

    $testStartAt = Get-Date
    $registrationId = "e2e-${ReleaseLabel}-Win-${Architecture}-DPS-$provisioningType"
    PrintHighlightedMessage "Run DPS provisioning test $provisioningType for registration id ""$registrationId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$registrationId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            --dps-scope-id `"$DpsScopeId`" ``
            $BypassInstallationFlag"

    switch ($provisioningType) {
        ([DpsProvisioningType]::SymmetricKey)
        {
            $testCommand = "$testCommand ``
                --dps-registration-id `"$registrationId`" ``
                --dps-master-symmetric-key `"$DpsMasterSymmetricKey`""
        }

        ([DpsProvisioningType]::Tpm)
        {
            $testCommand = "$testCommand ``
                --dps-registration-id `"$registrationId`""
        }

        ([DpsProvisioningType]::X509)
        {
            # for windows X.509 mutual auth clients to work, the expectation is that the root
            # and any intermediate certificates (public certs only) be installed in the certificate store
            $installCACommand = "Import-Certificate -CertStoreLocation 'cert:\LocalMachine\Root' -FilePath $EdgeCertGenScriptDir\certs\azure-iot-test-only.root.ca.cert.pem"
            Invoke-Expression $installCACommand | Out-Host
            $installCACommand = "Import-Certificate -CertStoreLocation 'cert:\LocalMachine\Root' -FilePath $EdgeCertGenScriptDir\certs\azure-iot-test-only.intermediate.cert.pem"
            Invoke-Expression $installCACommand | Out-Host

            New-CACertsDevice "$registrationId"
            $identityPkPath = "$EdgeCertGenScriptDir\private\iot-device-${registrationId}.key.pem"
            $identityCertPath = "$EdgeCertGenScriptDir\certs\iot-device-${registrationId}-full-chain.cert.pem"

            $testCommand = "$testCommand ``
                --device_identity_pk `"$identityPkPath`" ``
                --device_identity_cert `"$identityCertPath`""
        }
    }

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

Function RunQuickstartCertsTest
{
    PrintHighlightedMessage "Run Quickstart Certs test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-QuickstartCerts"
    PrintHighlightedMessage "Run quickstart certs test on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"doesNotNeed`" ``
        -n `"$env:computername`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" ``
        -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        --optimize_for_performance=`"$OptimizeForPerformance`" ``
        --leave-running=All ``
        --no-verify ``
        $BypassInstallationFlag"
    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            -l `"$DeploymentWorkingFilePath`" ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host

    $caCertPath = (Get-ChildItem $env:ProgramData\iotedge\hsm\certs\edge_owner_ca*.pem | Select -First 1).FullName
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

Function RunLongHaulTest
{
    PrintHighlightedMessage "Run Long Haul test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "${ReleaseLabel}-Windows-${Architecture}-longHaul"
    PrintHighlightedMessage "Run Long Haul test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            --leave-running=All ``
            -l `"$DeploymentWorkingFilePath`" ``
            --runtime-log-level `"Info`" ``
            --no-verify ``
            $BypassInstallationFlag"
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host
    $testExitCode = $LastExitCode

    PrintLogs $testStartAt $testExitCode
    Return $testExitCode
}

Function RunStressTest
{
    PrintHighlightedMessage "Run Stress test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "${ReleaseLabel}-Windows-${Architecture}-stress"
    PrintHighlightedMessage "Run Stress test with -d ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"doesNotNeed`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            --leave-running=All ``
            -l `"$DeploymentWorkingFilePath`" ``
            --runtime-log-level `"Info`" ``
            --no-verify ``
            $BypassInstallationFlag"
    $testCommand = AppendInstallationOption($testCommand)
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
    PrintHighlightedMessage "Run TempFilter test on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilter`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            $BypassInstallationFlag"
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
        PrintHighlightedMessage "Temp Filter Functions test is not supported for $Architecture"
        Return 0
    }

    PrintHighlightedMessage "Run TempFilterFunctions test for $Architecture"
    TestSetup

    $testStartAt = Get-Date
    $deviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-tempFilterFunc"
    PrintHighlightedMessage "Run Temp Filter Functions test on device ""$deviceId"" started at $testStartAt"

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
            -d `"$deviceId`" ``
            -c `"$IoTHubConnectionString`" ``
            -e `"$EventHubConnectionString`" ``
            -n `"$env:computername`" ``
            -r `"$ContainerRegistry`" ``
            -u `"$ContainerRegistryUsername`" ``
            -p `"$ContainerRegistryPassword`" --verify-data-from-module `"tempFilterFunctions`" ``
            -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
            -l `"$DeploymentWorkingFilePath`" ``
            $BypassInstallationFlag"
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
    PrintHighlightedMessage "Run TempSensor test on device ""$deviceId"" started at $testStartAt."

    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
        -d `"$deviceId`" ``
        -c `"$IoTHubConnectionString`" ``
        -e `"$EventHubConnectionString`" ``
        -n `"$env:computername`" ``
        -r `"$ContainerRegistry`" ``
        -u `"$ContainerRegistryUsername`" ``
        -p `"$ContainerRegistryPassword`" ``
        -t `"${ArtifactImageBuildNumber}-windows-$(GetImageArchitectureLabel)`" ``
        -tw `"$TwinTestFileArtifactFilePath`" ``
        --optimize_for_performance=`"$OptimizeForPerformance`" ``
        $BypassInstallationFlag"

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
    [string]$edgeDeviceId,
    [bool]$useSecondaryCredential = $False
)
{

    if ($leafDeviceId.Length -gt 64) {
        throw "can't generate cert. asn1 device name length of 64 exceeded. $leafDeviceId"
    }
    
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

            If ($useSecondaryCredential) {
                $testCommand = "$testCommand --use-secondary-credential"
            }

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

    TestSetup

    $testStartAt = Get-Date
    $edgeDeviceId = "e2e-${ReleaseLabel}-Windows-${Architecture}-TransGW"
    PrintHighlightedMessage "Run transparent gateway test on device ""$edgeDeviceId"" started at $testStartAt."

    # generate the edge gateway certs
    New-CACertsEdgeDevice $edgeDeviceId

    #launch the edge as a transparent gateway
    $testCommand = "&$IotEdgeQuickstartExeTestPath ``
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
        --optimize_for_performance=`"$OptimizeForPerformance`" ``
        --leave-running=All ``
        --no-verify ``
        $BypassInstallationFlag"

    If ($ProxyUri) {
        $testCommand = "$testCommand ``
            -l `"$DeploymentWorkingFilePath`" ``
            --upstream-protocol 'AmqpWs' ``
            --proxy `"$ProxyUri`""
    }
    $testCommand = AppendInstallationOption($testCommand)
    Invoke-Expression $testCommand | Out-Host

    # if the deployment of edge runtime and modules fails, then return immediately.
    If($LastExitCode -eq 1) {
      Return $LastExitCode
    }

    # run the various leaf device tests
    $deviceId = "e2e-${ReleaseLabel}-Win-${Architecture}"

    # NOTE: device name cannot be > 64 chars because of asn1 limits for certs
    # thus we're abbreviating noscope/inscope as no/in and leaf as lf, x509ca as x5c, x509th as x5t, pri as p, sec as s.
    RunLeafDeviceTest "sas" "Mqtt" "$deviceId-mqtt-sas-no-lf" $null
    RunLeafDeviceTest "sas" "Amqp" "$deviceId-amqp-sas-no-lf" $null

    RunLeafDeviceTest "sas" "Mqtt" "$deviceId-mqtt-sas-in-lf" $edgeDeviceId
    RunLeafDeviceTest "sas" "Amqp" "$deviceId-amqp-sas-in-lf" $edgeDeviceId

    RunLeafDeviceTest "x509CA" "Mqtt" "$deviceId-mqtt-x5c-in-lf" $edgeDeviceId
    RunLeafDeviceTest "x509CA" "Amqp" "$deviceId-amqp-x5c-in-lf" $edgeDeviceId

    # run thumbprint test using primary cert with MQTT
    RunLeafDeviceTest "x509Thumprint" "Mqtt" "$deviceId-mqtt-p-x5t-in-lf" $edgeDeviceId
    # run thumbprint test using secondary cert with MQTT
    RunLeafDeviceTest "x509Thumprint" "Mqtt" "$deviceId-mqtt-s-x5t-in-lf" $edgeDeviceId $True
    # run thumbprint test using primary cert with AMQP
    RunLeafDeviceTest "x509Thumprint" "Amqp" "$deviceId-amqp-p-x5t-in-lf" $edgeDeviceId
    # run thumbprint test using secondary cert with AMQP
    RunLeafDeviceTest "x509Thumprint" "Amqp" "$deviceId-amqp-s-x5t-in-lf" $edgeDeviceId $True

    Return $testExitCode
}

Function RunTest
{
    $testExitCode = 0

    If ($TestName -eq "DpsX509Provisioning" -Or $TestName -eq "TransparentGateway")
    {
        # setup certificate generation tools to create the Edge device and leaf device certificates
        PrepareCertificateTools

        # dot source the certificate generation script
        . "$EdgeCertGenScript"

        # install the provided root CA to seed the certificate chain
        If ($EdgeE2ERootCACertRSAFile -and $EdgeE2ERootCAKeyRSAFile)
        {
            Install-RootCACertificate $EdgeE2ERootCACertRSAFile $EdgeE2ERootCAKeyRSAFile "rsa" $EdgeE2ETestRootCAPassword
        }
    }

    Switch ($TestName)
    {
        "All" { $testExitCode = RunAllTests; break }
        "DirectMethodAmqp" { $testExitCode = RunDirectMethodAmqpTest; break }
        "DirectMethodAmqpMqtt" { $testExitCode = RunDirectMethodAmqpMqttTest; break }
        "DirectMethodMqtt" { $testExitCode = RunDirectMethodMqttTest; break }
        "DirectMethodMqttAmqp" { $testExitCode = RunDirectMethodMqttAmqpTest; break }
        "DpsSymmetricKeyProvisioning" { $testExitCode = RunDpsProvisioningTest ([DpsProvisioningType]::SymmetricKey); break }
        "DpsTpmProvisioning" { $testExitCode = RunDpsProvisioningTest ([DpsProvisioningType]::Tpm); break }
        "DpsX509Provisioning" { $testExitCode = RunDpsProvisioningTest ([DpsProvisioningType]::X509); break }
        "QuickstartCerts" { $testExitCode = RunQuickstartCertsTest; break }
        "LongHaul" { $testExitCode = RunLongHaulTest; break }
        "Stress" { $testExitCode = RunStressTest; break }
        "TempFilter" { $testExitCode = RunTempFilterTest; break }
        "TempFilterFunctions" { $testExitCode = RunTempFilterFunctionsTest; break }
        "TempSensor" { $testExitCode = RunTempSensorTest; break }
        "TransparentGateway" { $testExitCode = RunTransparentGatewayTest; break }
        default { Throw "$TestName test is not supported." }
    }

    Return $testExitCode
}

Function SetEnvironmentVariable
{
    # IotEdgeQuickstart runs different processes to call iotedge list right after running installation script.
    # E2E test failed randomly when running iotedge list command throws Win32Exception as Path environment variable may not be in place yet.
    # Therefore set it explicitly before running each test.
    $env:Path="$env:Path;C:\Program Files\iotedge-moby;C:\Program Files\iotedge"
}

Function TestSetup
{
    Write-Host "Environment setup..."
    ValidateTestParameters
    If (!$BypassEdgeInstallation)
    {
        # Cleanup/Setup
        $testCommand = "&$EnvSetupScriptPath ``
            -ArtifactImageBuildNumber `"$ArtifactImageBuildNumber`" ``
            -E2ETestFolder `"$E2ETestFolder`""

        Invoke-Expression $testCommand | Out-Host
    }
    InitializeWorkingFolder
    PrepareTestFromArtifacts
    SetEnvironmentVariable
}

Function ValidateTestParameters
{
    PrintHighlightedMessage "Validate test parameters for $TestName"

    If (-Not((Test-Path (Join-Path $IoTEdgedArtifactFolder "*")) -Or (Test-Path (Join-Path $PackagesArtifactFolder "*"))))
    {
        Throw "Either $IoTEdgedArtifactFolder or $PackagesArtifactFolder should exist"
    }

    $validatingItems = @(
        (Join-Path $IotEdgeQuickstartArtifactFolder "*"),
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

    If ($TestName -eq "LongHaul")
    {
        $validatingItems += $LongHaulDeploymentArtifactFilePath
    }

    If ($TestName -eq "Stress")
    {
        $validatingItems += $StressDeploymentArtifactFilePath
    }

    $validatingItems | ForEach-Object {
        If (-Not (Test-Path -Path $_))
        {
            Throw "$_ is not found or it is empty"
        }
    }

    If ($TestName -eq "LongHaul" -Or $TestName -eq "Stress")
    {
        If ([string]::IsNullOrEmpty($SnitchAlertUrl)) {Throw "Required snith alert URL."}
        If ([string]::IsNullOrEmpty($SnitchStorageAccount)) {Throw "Required snitch storage account."}
        If ([string]::IsNullOrEmpty($SnitchStorageMasterKey)) {Throw "Required snitch storage master key."}
        If ($ProxyUri) {Throw "Proxy not supported for $TestName test"}
    }
}

Function PrintHighlightedMessage
{
    param ([string] $heading)

    Write-Host -f Cyan $heading
}

$Architecture = GetArchitecture
$OptimizeForPerformance=$True
If ($Architecture -eq "arm32v7")
{
    $OptimizeForPerformance=$False
}
$E2ETestFolder = (Resolve-Path $E2ETestFolder).Path
$DefaultOpensslInstallPath = "C:\vcpkg\installed\x64-windows\tools\openssl"
$EnvSetupScriptPath = Join-Path $E2ETestFolder "artifacts\core-windows\scripts\windows\test\Setup-Env.ps1"
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
$LongHaulDeploymentFilename = GetLongHaulDeploymentFilename
$StressDeplymentFilename = GetStressDeploymentFilename

$IotEdgeQuickstartArtifactFolder = Join-Path $E2ETestFolder "artifacts\core-windows\IotEdgeQuickstart\$Architecture"
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
$LongHaulDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $LongHaulDeploymentFilename
$StressDeploymentArtifactFilePath = Join-Path $DeploymentFilesFolder $StressDeplymentFilename

$TestWorkingFolder = Join-Path $E2ETestFolder "working"
$QuickstartWorkingFolder = (Join-Path $TestWorkingFolder "quickstart")
$LeafDeviceWorkingFolder = (Join-Path $TestWorkingFolder "leafdevice")
$IoTEdgedWorkingFolder = (Join-Path $TestWorkingFolder "iotedged")
$PackagesWorkingFolder = (Join-Path $TestWorkingFolder "packages")
$IotEdgeQuickstartExeTestPath = (Join-Path $QuickstartWorkingFolder "IotEdgeQuickstart.exe")
$LeafDeviceExeTestPath = (Join-Path $LeafDeviceWorkingFolder "LeafDevice.exe")
$DeploymentWorkingFilePath = Join-Path $TestWorkingFolder "deployment.json"

If ([string]::IsNullOrWhiteSpace($EdgeE2ERootCACertRSAFile))
{
    $EdgeE2ERootCACertRSAFile=$DefaultInstalledRSARootCACert
}

If ([string]::IsNullOrWhiteSpace($EdgeE2ERootCAKeyRSAFile))
{
    $EdgeE2ERootCAKeyRSAFile=$DefaultInstalledRSARootCAKey
}

If ([string]::IsNullOrWhiteSpace($TwinUpdateFailureThreshold))
{
    $TwinUpdateFailureThreshold="00:00:01"
}

If ([string]::IsNullOrWhiteSpace($MetricsScrapeFrequencyInSecs))
{
    $MetricsScrapeFrequencyInSecs=300;
}

If ([string]::IsNullOrWhiteSpace($MetricsUploadTarget))
{
    $MetricsScrapeFrequencyInSecs="AzureLogAnalytics";
}

If ($TestName -eq "LongHaul")
{
    If ([string]::IsNullOrEmpty($DesiredModulesToRestartCSV)) {$DesiredModulesToRestartCSV = ","}
    If ([string]::IsNullOrEmpty($LoadGenMessageFrequency)) {$LoadGenMessageFrequency = "00:00:01"}
    If ([string]::IsNullOrEmpty($RestartIntervalInMins)) {$RestartIntervalInMins = "10"}
    If ([string]::IsNullOrEmpty($SnitchReportingIntervalInSecs)) {$SnitchReportingIntervalInSecs = "86400"}
    If ([string]::IsNullOrEmpty($SnitchTestDurationInSecs)) {$SnitchTestDurationInSecs = "604800"}
    If ([string]::IsNullOrEmpty($TwinUpdateFrequency)) {$TwinUpdateSize = "00:00:15"}
    If ([string]::IsNullOrEmpty($TwinUpdateSize)) {$TwinUpdateSize = "1"}
}

If ($TestName -eq "Stress")
{
    If ([string]::IsNullOrEmpty($LoadGenMessageFrequency)) {$LoadGenMessageFrequency = "00:00:00.03"}
    If ([string]::IsNullOrEmpty($SnitchReportingIntervalInSecs)) {$SnitchReportingIntervalInSecs = "1700000"}
    If ([string]::IsNullOrEmpty($SnitchTestDurationInSecs)) {$SnitchTestDurationInSecs = "14400"}
    If ([string]::IsNullOrEmpty($TwinUpdateFrequency)) {$TwinUpdateSize = "00:00:01"}
    If ([string]::IsNullOrEmpty($TwinUpdateSize)) {$TwinUpdateSize = "100"}
}

If ($BypassEdgeInstallation)
{
    $BypassInstallationFlag = "--bypass-edge-installation"
}
Else
{
    $BypassInstallationFlag = $null
}

# Evaluate collection of test results for final pass/fail result
RunTest | ForEach-Object {$retCode = 0} {$retCode = $retCode -bor $_}
Write-Host "Exit test with code $retCode"

Exit $retCode -gt 0
