New-Module -Name IoTEdge -ScriptBlock {

<#
 # Installs the IoT Edge Security Daemon on Windows.
 #>

#requires -Version 5
#requires -RunAsAdministrator

Set-Variable Windows1607 -Value 14393 -Option Constant
Set-Variable Windows1809 -Value 17763 -Option Constant

Set-Variable MinBuildForLinuxContainers -Value $Windows1607

# When using Windows containers, the host OS version must match the container OS version.
# Since our containers are built with 10.0.17763 base images, we require the same for the host OS.
#
# If this needs to be changed, also update the host OS version check in the `iotedge check` tool (edgelet/iotedge/src/check/mod.rs)
Set-Variable SupportedBuildsForWindowsContainers -Value @($Windows1809)

Set-Variable DockerServiceName -Value 'com.docker.service' -Option Constant

Set-Variable EdgePackage -Value 'microsoft-azure-iotedge' -Option Constant

# If the user is running a 32-bit PS host on a 64-bit OS, then `$env:ProgramFiles` points to `C:\Program Files (x86)`
# So use `$env:ProgramW6432` instead.
#
# However, an actual 32-bit OS like IoT Core ARM32 does not define `$env:ProgramW6432`, so fall back to `$env:ProgramFiles` in that case.
Set-Variable ProgramFilesDirectory -Value $(
    if (Test-Path Env:\ProgramW6432) {
        $env:ProgramW6432
    }
    else {
        $env:ProgramFiles
    }
) -Option Constant

Set-Variable EdgeInstallDirectory -Value "$ProgramFilesDirectory\iotedge" -Option Constant
Set-Variable EdgeDataDirectory -Value "$env:ProgramData\iotedge" -Option Constant
Set-Variable EdgeServiceName -Value 'iotedge' -Option Constant

Set-Variable ContainersFeaturePackageName -Value 'Microsoft-IoT-Containers-Server-Package' -Option Constant
Set-Variable ContainersFeatureLangPackageName -Value 'Microsoft-IoT-Containers-Server-Package_*' -Option Constant

Set-Variable MobyDataRootDirectory -Value "$env:ProgramData\iotedge-moby" -Option Constant
Set-Variable MobyInstallDirectory -Value "$ProgramFilesDirectory\iotedge-moby" -Option Constant
Set-Variable MobyLinuxNamedPipeUrl -Value 'npipe://./pipe/docker_engine' -Option Constant
Set-Variable MobyNamedPipeUrl -Value 'npipe://./pipe/iotedge_moby_engine' -Option Constant
Set-Variable MobyServiceName -Value 'iotedge-moby' -Option Constant

Set-Variable LegacyEdgeInstallDirectory -Value 'C:\ProgramData\iotedge' -Option Constant
Set-Variable LegacyEdgeEventLogName -Value 'iotedged' -Option Constant
Set-Variable LegacyEdgeEventLogInstallDirectory -Value 'C:\ProgramData\iotedge-eventlog' -Option Constant
Set-Variable LegacyEventLogApplicationRegPath -Value 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application' -Option Constant
Set-Variable LegacyMobyDataRootDirectory -Value "$env:ProgramData\iotedge-moby-data" -Option Constant
Set-Variable LegacyMobyStaticDataRootDirectory -Value 'C:\ProgramData\iotedge-moby-data' -Option Constant
Set-Variable LegacyMobyInstallDirectory -Value "$env:ProgramData\iotedge-moby" -Option Constant
Set-Variable LegacyMobyStaticInstallDirectory -Value 'C:\ProgramData\iotedge-moby' -Option Constant

Set-Variable ReinstallMessage -Value 'To reinstall, first remove the existing installation using "Uninstall-IoTEdge".' -Option Constant
Set-Variable InstallMessage -Value 'To install, run "Deploy-IoTEdge" first.' -Option Constant

enum ContainerOs {
    Linux
    Windows
}

<#
.SYNOPSIS

Initializes the IoT Edge Security Daemon and its dependencies.


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Initialize-IoTEdge -Manual -DeviceConnectionString $deviceConnectionString -ContainerOs Windows


.EXAMPLE

PS> Initialize-IoTEdge -Manual -DeviceConnectionString $deviceConnectionString -ContainerOs Windows -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -SymmetricKey $symmetricKey


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -SymmetricKey $symmetricKey -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -ContainerOs Windows -X509IdentityCertificate $x509IdentityCertificate
-X509IdentityPrivateKey $x509IdentityPrivateKey


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -ContainerOs Windows -X509IdentityCertificate $x509IdentityCertificate
-X509IdentityPrivateKey $x509IdentityPrivateKey -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Initialize-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -AutoGenX509IdentityCertificate $true -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Initialize-IoTEdge -External -ExternalProvisioningEndpoint $externalProvisioningEndpoint -ContainerOs Windows -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle
#>
function Initialize-IoTEdge {
    [CmdletBinding(DefaultParameterSetName = 'Manual')]
    param (
        # Specified the daemon will be configured manually, using a device connection string.
        [Parameter(ParameterSetName = 'Manual')]
        [Switch] $Manual,

        # Specified the daemon will be configured using DPS, using a scope ID and registration ID.
        [Parameter(ParameterSetName = 'DPS')]
        [Switch] $Dps,

        # Specified the daemon will be configured using an external provisioning endpoint.
        [Parameter(ParameterSetName = 'External')]
        [Switch] $External,

        # The device connection string.
        [Parameter(Mandatory = $true, ParameterSetName = 'Manual')]
        [String] $DeviceConnectionString,

        # The DPS scope ID.
        [Parameter(Mandatory = $true, ParameterSetName = 'DPS')]
        [String] $ScopeId,

        # The DPS registration ID.
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $RegistrationId,

        # The DPS symmetric key to provision the Edge device identity
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $SymmetricKey,

        # The Edge device identity certificate
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $X509IdentityCertificate,

        # The Edge device identity private key
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $X509IdentityPrivateKey,

        # Auto generate the X.509 identity certificate from the device CA
        [Parameter(ParameterSetName = 'DPS')]
        [bool] $AutoGenX509IdentityCertificate = $false,

        # The Edge device CA certificate
        [ValidateNotNullOrEmpty()]
        [String] $DeviceCACertificate,

        # The Edge device CA private key
        [ValidateNotNullOrEmpty()]
        [String] $DeviceCAPrivateKey,

        # The Edge device trustbundle
        [ValidateNotNullOrEmpty()]
        [String] $DeviceTrustbundle,

        # The external provisioning environment endpoint for the External provisioning mode.
        [Parameter(Mandatory = $true, ParameterSetName = 'External')]
        [ValidateNotNullOrEmpty()]
        [String] $ExternalProvisioningEndpoint,

        # The base OS of all the containers that will be run on this device via the security daemon.
        #
        # If set to Linux, a separate installation of Docker for Windows is expected.
        #
        # If set to Windows, the Moby Engine will be installed automatically. This will not affect any existing installation of Docker for Windows.
        [ContainerOs] $ContainerOs = 'Windows',

        # IoT Edge Agent image to pull for the initial configuration.
        [String] $AgentImage,

        # Username used to access the container registry and pull the IoT Edge Agent image.
        [String] $Username,

        # Password used to access the container registry and pull the IoT Edge Agent image.
        [SecureString] $Password
    )

    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version 5

    if (-not (Test-EdgeAlreadyInstalled)) {
        Write-HostRed
        Write-HostRed ('IoT Edge is not yet installed. ' + $InstallMessage)
        throw
    }

    if ((Test-MobyNeedsToBeMoved) -or (Test-LegacyInstaller)) {
        Write-HostRed
        Write-HostRed ('IoT Edge is installed in an invalid location. ' + $ReinstallMessage)
        throw
    }

    if (-not (Test-MobyAlreadyInstalled)) {
        Write-HostRed
        Write-HostRed ('IoT Edge Moby Engine is not yet installed. ' + $ReinstallMessage)
        throw
    }

    if (-not (Test-AgentRegistryArgs)) {
        throw
    }

    Setup-Environment -ContainerOs $ContainerOs -SkipArchCheck -SkipBatteryCheck

    $configPath = Join-Path -Path $EdgeDataDirectory -ChildPath 'config.yaml'
    if (Test-Path $configPath) {
        Write-HostRed
        Write-HostRed "$configPath already exists."
        Write-HostRed ('Delete it using "Uninstall-IoTEdge -Force" and then ' +
            're-run "Deploy-IoTEdge" and "Initialize-IoTEdge"')
        throw
    }

    # config.yaml
    Write-Host 'Generating config.yaml...'

    if (-not (Test-Path $EdgeDataDirectory)) {
        New-Item -Path $EdgeDataDirectory -ItemType 'Directory'
    }
    Copy-Item -Path (Join-Path -Path $EdgeInstallDirectory -ChildPath 'config.yaml') -Destination $configPath

    Set-ProvisioningMode
    Set-Certificates
    Set-AgentImage
    Set-Hostname
    if ($ContainerOs -eq 'Linux') {
        Set-GatewayAddress
    }
    else {
        Set-CorrectProgramData
    }
    Set-MobyEngineParameters

    # Start services
    Set-SystemPath
    Start-IoTEdgeService
    if ($ContainerOs -eq 'Linux') {
        Add-FirewallExceptions
    }

    Write-LogInformation
}

<#
.SYNOPSIS

Updates the IoT Edge Security Daemon and its dependencies.


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Update-IoTEdge


.EXAMPLE

PS> Update-IoTEdge -OfflineInstallationPath c:\data
#>
function Update-IoTEdge {
    [CmdletBinding()]
    param (
        # The base OS of all the containers that will be run on this device via the security daemon.
        #
        # If set to Linux, a separate installation of Docker for Windows is expected.
        #
        # If set to Windows, the Moby Engine will be installed automatically. This will not affect any existing installation of Docker for Windows.
        [ContainerOs] $ContainerOs = 'Windows',

        # Proxy URI used for all Invoke-WebRequest calls. To specify other proxy-related options like -ProxyCredential, see -InvokeWebRequestParameters
        [Uri] $Proxy,

        # If set to a directory path, the installer prefers to use IoTEdge CAB and VC Runtime MSI files from inside this directory
        # over downloading them from the internet. Thus placing both files in this directory can be used to have a completely offline install,
        # or a specific component's file can be placed to override the online file corresponding to that specific component.
        [String] $OfflineInstallationPath,

        # Splatted into every Invoke-WebRequest invocation. Can be used to set extra options.
        #
        # If -Proxy is also specified, it overrides the "-Proxy" key set in this hashtable, if any.
        #
        # Example:
        #
        #     Update-IoTEdge -InvokeWebRequestParameters @{ '-Proxy' = 'http://localhost:8888'; '-ProxyCredential' = (Get-Credential).GetNetworkCredential() }
        [HashTable] $InvokeWebRequestParameters,

        # Restart if needed without prompting.
        [Switch] $RestartIfNeeded
    )

    Install-Packages `
        -ContainerOs $ContainerOs `
        -Proxy $Proxy `
        -OfflineInstallationPath $OfflineInstallationPath `
        -InvokeWebRequestParameters $InvokeWebRequestParameters `
        -RestartIfNeeded:$RestartIfNeeded `
        -Update `
        -SkipArchCheck `
        -SkipBatteryCheck
}

<#
.SYNOPSIS

Deploys the IoT Edge Security Daemon and its dependencies.


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Deploy-IoTEdge


.EXAMPLE

PS> Deploy-IoTEdge -OfflineInstallationPath c:\data
#>
function Deploy-IoTEdge {
    [CmdletBinding()]
    param (
        # The base OS of all the containers that will be run on this device via the security daemon.
        #
        # If set to Linux, a separate installation of Docker for Windows is expected.
        #
        # If set to Windows, the Moby Engine will be installed automatically. This will not affect any existing installation of Docker for Windows.
        [ContainerOs] $ContainerOs = 'Windows',

        # Proxy URI used for all Invoke-WebRequest calls. To specify other proxy-related options like -ProxyCredential, see -InvokeWebRequestParameters
        [Uri] $Proxy,

        # If set to a directory path, the installer prefers to use IoTEdge CAB and VC Runtime MSI files from inside this directory
        # over downloading them from the internet. Thus placing both files in this directory can be used to have a completely offline install,
        # or a specific component's file can be placed to override the online file corresponding to that specific component.
        [String] $OfflineInstallationPath,

        # Splatted into every Invoke-WebRequest invocation. Can be used to set extra options.
        #
        # If -Proxy is also specified, it overrides the "-Proxy" key set in this hashtable, if any.
        #
        # Example:
        #
        #     Update-IoTEdge -InvokeWebRequestParameters @{ '-Proxy' = 'http://localhost:8888'; '-ProxyCredential' = (Get-Credential).GetNetworkCredential() }
        [HashTable] $InvokeWebRequestParameters,

        # Restart if needed without prompting.
        [Switch] $RestartIfNeeded,

        # Skip processor architecture check.
        [Switch] $SkipArchCheck,

        # Skip battery check.
        [Switch] $SkipBatteryCheck
    )

    Install-Packages `
        -ContainerOs $ContainerOs `
        -Proxy $Proxy `
        -OfflineInstallationPath $OfflineInstallationPath `
        -InvokeWebRequestParameters $InvokeWebRequestParameters `
        -RestartIfNeeded:$RestartIfNeeded `
        -SkipArchCheck:$SkipArchCheck `
        -SkipBatteryCheck:$SkipBatteryCheck
}

<#
.SYNOPSIS

Installs the IoT Edge Security Daemon and its dependencies. Wrapper for Deploy-IoTEdge and Initialize-IoTEdge.


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Install-IoTEdge -Manual -DeviceConnectionString $deviceConnectionString -ContainerOs Windows


.EXAMPLE

PS> Install-IoTEdge -Manual -DeviceConnectionString $deviceConnectionString -ContainerOs Windows -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -SymmetricKey $symmetricKey


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -SymmetricKey $symmetricKey -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -ContainerOs Windows -X509IdentityCertificate $x509IdentityCertificate -X509IdentityPrivateKey $x509IdentityPrivateKey


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -ContainerOs Windows -X509IdentityCertificate $x509IdentityCertificate -X509IdentityPrivateKey $x509IdentityPrivateKey -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Install-IoTEdge -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows -AutoGenX509IdentityCertificate $true -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle


.EXAMPLE

PS> Install-IoTEdge -External -ExternalProvisioningEndpoint $externalProvisioningEndpoint -ContainerOs Windows -DeviceCACertificate $deviceCACertificate -DeviceCAPrivateKey $deviceCAPrivateKey -DeviceTrustbundle $deviceTrustbundle
#>
function Install-IoTEdge {
    [CmdletBinding(DefaultParameterSetName = 'Manual')]
    param (
        # Specified the daemon will be configured manually, using a device connection string.
        [Parameter(ParameterSetName = 'Manual')]
        [Switch] $Manual,

        # Specified the daemon will be configured using DPS, using a scope ID and registration ID.
        [Parameter(ParameterSetName = 'DPS')]
        [Switch] $Dps,

        # Specified the daemon will be configured using an external provisioning endpoint.
        [Parameter(ParameterSetName = 'External')]
        [Switch] $External,

        # The device connection string.
        [Parameter(Mandatory = $true, ParameterSetName = 'Manual')]
        [String] $DeviceConnectionString,

        # The DPS scope ID.
        [Parameter(Mandatory = $true, ParameterSetName = 'DPS')]
        [String] $ScopeId,

        # The DPS registration ID.
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $RegistrationId,

        # The DPS symmetric key to provision the Edge device identity
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $SymmetricKey,

        # The Edge device identity certificate
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $X509IdentityCertificate,

        # The Edge device identity private key
        [Parameter(ParameterSetName = 'DPS')]
        [ValidateNotNullOrEmpty()]
        [String] $X509IdentityPrivateKey,

        # Auto generate the X.509 identity certificate from the device CA
        [Parameter(ParameterSetName = 'DPS')]
        [bool] $AutoGenX509IdentityCertificate = $false,

        # The Edge device CA certificate
        [ValidateNotNullOrEmpty()]
        [String] $DeviceCACertificate,

        # The Edge device CA private key
        [ValidateNotNullOrEmpty()]
        [String] $DeviceCAPrivateKey,

        # The Edge device trustbundle
        [ValidateNotNullOrEmpty()]
        [String] $DeviceTrustbundle,

        # The external provisioning environment endpoint for the External provisioning mode.
        [Parameter(Mandatory = $true, ParameterSetName = 'External')]
        [ValidateNotNullOrEmpty()]
        [String] $ExternalProvisioningEndpoint,

        # The base OS of all the containers that will be run on this device via the security daemon.
        #
        # If set to Linux, a separate installation of Docker for Windows is expected.
        #
        # If set to Windows, the Moby Engine will be installed automatically. This will not affect any existing installation of Docker for Windows.
        [ContainerOs] $ContainerOs = 'Windows',

        # Proxy URI used for all Invoke-WebRequest calls. To specify other proxy-related options like -ProxyCredential, see -InvokeWebRequestParameters
        [Uri] $Proxy,

        # If set to a directory path, the installer prefers to use IoTEdge CAB, Moby Engine CAB, Moby CLI CAB and VC Runtime MSI files from inside this directory
        # over downloading them from the internet. Thus placing all four files in this directory can be used to have a completely offline install,
        # or a specific subset can be placed to override the online versions of those specific components.
        [String] $OfflineInstallationPath,

        # IoT Edge Agent image to pull for the initial configuration.
        [String] $AgentImage,

        # Username used to access the container registry and pull the IoT Edge Agent image.
        [String] $Username,

        # Password used to access the container registry and pull the IoT Edge Agent image.
        [SecureString] $Password,

        # Splatted into every Invoke-WebRequest invocation. Can be used to set extra options.
        #
        # If -Proxy is also specified, it overrides the "-Proxy" key set in this hashtable, if any.
        #
        # Example:
        #
        #     Update-IoTEdge -InvokeWebRequestParameters @{ '-Proxy' = 'http://localhost:8888'; '-ProxyCredential' = (Get-Credential).GetNetworkCredential() }
        [HashTable] $InvokeWebRequestParameters,

        # Restart if needed without prompting.
        [Switch] $RestartIfNeeded,

        # Skip processor architecture check.
        [Switch] $SkipArchCheck,

        # Skip battery check.
        [Switch] $SkipBatteryCheck
    )

    # Set by Deploy-IoTEdge if it succeeded, so we can abort early in case of failure.
    #
    # We use a script-scope var instead of having Deploy-IoTEdge return a boolean or take a [ref] parameter
    # because users can also run Deploy-IoTEdge themselves, so it can't be part of the public API.
    $script:installPackagesCompleted = $false

    # Used to suppress some messages from Deploy-IoTEdge since we are automatically running Initialize-IoTEdge
    $calledFromInstall = $true

    Deploy-IoTEdge `
        -ContainerOs $ContainerOs `
        -Proxy $Proxy `
        -OfflineInstallationPath $OfflineInstallationPath `
        -InvokeWebRequestParameters $InvokeWebRequestParameters `
        -RestartIfNeeded:$RestartIfNeeded `
        -SkipArchCheck:$SkipArchCheck `
        -SkipBatteryCheck:$SkipBatteryCheck

    if (-not $script:installPackagesCompleted) {
        return
    }

    $Params = @{
        '-ContainerOs' = $ContainerOs
    }

    if ($Manual) { $Params["-Manual"] = $true }
    if ($Dps) { $Params["-Dps"] = $true }
    if ($External) { $Params["-External"] = $true }
    if ($DeviceConnectionString) { $Params["-DeviceConnectionString"] = $DeviceConnectionString }
    if ($ScopeId) { $Params["-ScopeId"] = $ScopeId }
    if ($RegistrationId) { $Params["-RegistrationId"] = $RegistrationId }
    if ($SymmetricKey) { $Params["-SymmetricKey"] = $SymmetricKey }
    if ($X509IdentityCertificate) { $Params["-X509IdentityCertificate"] = $X509IdentityCertificate }
    if ($X509IdentityPrivateKey) { $Params["-X509IdentityPrivateKey"] = $X509IdentityPrivateKey }
    if ($AutoGenX509IdentityCertificate) { $Params["-AutoGenX509IdentityCertificate"] = $AutoGenX509IdentityCertificate }
    if ($DeviceCACertificate) { $Params["-DeviceCACertificate"] = $DeviceCACertificate }
    if ($DeviceCAPrivateKey) { $Params["-DeviceCAPrivateKey"] = $DeviceCAPrivateKey }
    if ($DeviceTrustbundle) { $Params["-DeviceTrustbundle"] = $DeviceTrustbundle }
    if ($ExternalProvisioningEndpoint) { $Params["-ExternalProvisioningEndpoint"] = $ExternalProvisioningEndpoint }
    if ($AgentImage) { $Params["-AgentImage"] = $AgentImage }
    if ($Username) { $Params["-Username"] = $Username }
    if ($Password) { $Params["-Password"] = $Password }

    # Used to suppress some messages from Initialize-IoTEdge that have already been emitted by Deploy-IoTEdge
    $initializeCalledFromInstall = $true

    Initialize-IoTEdge @Params
}

<#
.SYNOPSIS

Uninstalls the IoT Edge Security Daemon and its dependencies.


.DESCRIPTION

This cmdlet will delete the config.yaml and the Moby Engine data root (for -ContainerOs 'Windows' installs).


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Uninstall-IoTEdge


.EXAMPLE

PS> Uninstall-IoTEdge -Force
#>
function Uninstall-IoTEdge {
    [CmdletBinding()]
    param (
        # Forces the uninstallation in case the previous install was only partially successful.
        [Switch] $Force,

        # Restart if needed without prompting.
        [Switch] $RestartIfNeeded
    )

    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version 5

    $legacyInstaller = Test-LegacyInstaller

    if ((Test-IoTCore) -and (-not $legacyInstaller)) {
        Write-HostRed
        Write-HostRed ('Uninstall-IoTEdge is only supported on IoTCore to uninstall legacy installation. ' +
            'For new installations, please use "Update-IoTEdge" directly to update.')
        throw
    }

    if (-not $Force -and -not ((Test-EdgeAlreadyInstalled) -or (Test-MobyAlreadyInstalled))) {
        Write-HostRed
        Write-HostRed 'IoT Edge is not installed. Use "-Force" to uninstall anyway.'
        throw
    }

    Write-Host 'Uninstalling...'

    $ContainerOs = Get-ContainerOs

    $restartNeeded = $false
    Uninstall-Services -RestartNeeded ([ref] $restartNeeded) -LegacyInstaller $legacyInstaller
    $success = Remove-IoTEdgeResources -LegacyInstaller $legacyInstaller
    Reset-SystemPath

    Remove-MachineEnvironmentVariable 'IOTEDGE_HOST'
    Remove-Item Env:\IOTEDGE_HOST -ErrorAction SilentlyContinue

    Remove-FirewallExceptions

    if ($success) {
        Write-HostGreen 'Successfully uninstalled IoT Edge.'
    }

    if ($restartNeeded) {
        Write-HostRed 'Reboot required.'
        Write-Host 'You might need to rerun "Uninstall-IoTEdge" after the reboot to finish the cleanup.'
        Restart-Computer -Confirm:(-not $RestartIfNeeded) -Force:$RestartIfNeeded
    }
}

function Get-IoTEdgeLog {
    [CmdletBinding()]
    param (
        # What time to start the log from.
        [DateTime] $StartTime = [datetime]::Now.AddMinutes(-5)
    )

    Get-WinEvent -ea SilentlyContinue -FilterHashtable @{ProviderName='iotedged';LogName='application';StartTime=$StartTime} |
        Select TimeCreated, Message |
        Sort-Object @{Expression='TimeCreated';Descending=$false} |
        Format-Table -AutoSize -Wrap
}

function Install-Packages(
        [ContainerOs] $ContainerOs,
        [Uri] $Proxy,
        [String] $OfflineInstallationPath,
        [HashTable] $InvokeWebRequestParameters,
        [Switch] $RestartIfNeeded,
        [Switch] $Update,
        [Switch] $SkipArchCheck,
        [Switch] $SkipBatteryCheck
    )
{
    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version 5

    if ($InvokeWebRequestParameters -eq $null) {
        $InvokeWebRequestParameters = @{}
    }

    if ($Proxy -ne $null) {
        $InvokeWebRequestParameters['-Proxy'] = $Proxy
    }

    if ($Update) {
        if (-not (Test-EdgeAlreadyInstalled)) {
            Write-HostRed
            Write-HostRed ('IoT Edge is not yet installed. ' + $InstallMessage)
            throw
        }

        if ((Test-MobyNeedsToBeMoved) -or (Test-LegacyInstaller)) {
            Write-HostRed
            Write-HostRed ('IoT Edge is installed in an invalid location. ' + $ReinstallMessage)
            throw
        }

        if (-not (Test-MobyAlreadyInstalled)) {
            Write-HostRed
            Write-HostRed ('IoT Edge Moby Engine is not yet installed. ' + $ReinstallMessage)
            throw
        }
    }
    else {
        if (Test-EdgeAlreadyInstalled) {
            if ((Test-MobyNeedsToBeMoved) -or (Test-LegacyInstaller)) {
                Write-HostRed
                Write-HostRed ('IoT Edge is installed in an invalid location. ' + $ReinstallMessage)
            }
            else {
                Write-HostRed
                Write-HostRed ('IoT Edge is already installed. To update, run "Update-IoTEdge". ' +
                    'Alternatively, if you want to finalize the installation, run "Initialize-IoTEdge".')
            }
            throw
        }

        if (Test-MobyAlreadyInstalled) {
            if ((Test-MobyNeedsToBeMoved) -or (Test-LegacyInstaller)) {
                Write-HostRed
                Write-HostRed ('IoT Edge Moby Engine is installed in an invalid location. ' +
                    $ReinstallMessage)
            }
            else {
                Write-HostRed
                Write-HostRed ('IoT Edge Moby Engine is already installed, but IoT Edge is not. ' +
                    $ReinstallMessage)
            }
            throw
        }
    }

    Setup-Environment -ContainerOs $ContainerOs -SkipArchCheck:$SkipArchCheck -SkipBatteryCheck:$SkipBatteryCheck

    $restartNeeded = $false

    if (-not $Update) {
        if (-not (Test-IotCore)) {
            $result = Get-WindowsOptionalFeature -Online -FeatureName 'Containers'
            if ($result -and ($result.State -ne 'Enabled')) {
                $result = Enable-WindowsOptionalFeature -FeatureName 'Containers' -Online -NoRestart
                if ($result.RestartNeeded) {
                    $restartNeeded = $true
                }
            }
        }
        Get-VcRuntime
    }

    # Download
    Get-IoTEdge -RestartNeeded ([ref] $restartNeeded) -Update $Update
    if ((-not $restartNeeded) -and $Update) {
        try {
            Start-Service $EdgeServiceName
        }
        catch {
            Write-HostRed
            Write-HostRed 'Failed to start Security Daemon, make sure to initialize config file by running "Initialize-IoTEdge".'
            throw
        }
    }

    if ($Update) {
        Write-LogInformation
    }
    elseif (-not (Test-Path Variable:\calledFromInstall)) {
        Write-Host 'To complete the installation, run "Initialize-IoTEdge".'
    }

    if ($restartNeeded) {
        Write-HostRed 'Reboot required. To complete the installation after the reboot, run "Initialize-IoTEdge".'
        Restart-Computer -Confirm:(-not $RestartIfNeeded) -Force:$RestartIfNeeded
    }
    else {
        $script:installPackagesCompleted = $true
    }
}

function Setup-Environment {
    [CmdletBinding()]
    param ([string] $ContainerOs, [switch] $SkipArchCheck, [switch] $SkipBatteryCheck)

    $currentWindowsBuild = Get-WindowsBuild
    $preRequisitesMet = switch ($ContainerOs) {
        'Linux' {
            if ($currentWindowsBuild -lt $MinBuildForLinuxContainers) {
                Write-HostRed "The container host is on unsupported build version $currentWindowsBuild."
                Write-HostRed "Please use a container host running build $MinBuildForLinuxContainers or newer for running Linux containers."
                $false
            }
            elseif (-not (Test-IsDockerRunning)) {
                $false
            }
            else {
                if (-not (Test-Path Variable:\initializeCalledFromInstall)) {
                    Write-Warning ('Linux containers on Windows can be used for development and testing, ' +
                        'but are not supported in production IoT Edge deployments. See https://aka.ms/iotedge-platsup for more details.')
                }

                $true
            }
        }

        'Windows' {
            if ($SupportedBuildsForWindowsContainers -notcontains $currentWindowsBuild) {
                Write-HostRed "The container host is on unsupported build version $currentWindowsBuild."
                Write-HostRed 'Please use a container host running one of the following build versions for running Windows containers:'
                foreach ($version in $SupportedBuildsForWindowsContainers) {
                    Write-HostRed "$version"
                }

                $false
            }
            else {
                $true
            }
        }
    }

    if ((-not $SkipArchCheck) -and ($env:PROCESSOR_ARCHITECTURE -eq 'ARM')) {
        Write-HostRed ('IoT Edge is currently not supported on Windows ARM32. ' +
            'See https://aka.ms/iotedge-platsup for more details.')
        $preRequisitesMet = $false
    }

    if (Test-IoTCore) {
        if (-not (Get-Service vmcompute -ErrorAction SilentlyContinue) -or (-not [bool] (Get-Package $ContainersFeaturePackageName)) -or (-not [bool] (Get-Package $ContainersFeatureLangPackageName))) {
            Write-HostRed "The container host does not have 'Containers Feature' enabled. Please build an Iot Core image with 'Containers Feature' enabled."
            $preRequisitesMet = $false
        }
    }

    if ($preRequisitesMet) {
        Write-HostGreen "The container host is on supported build version $currentWindowsBuild."
        Set-ContainerOs
    }
    else {
        Write-HostRed
        Write-HostRed ('The prerequisites for installation of the IoT Edge Security daemon are not met. ' +
            'Please fix all known issues before rerunning this script.')
        throw
    }

    if (-not (Test-IotCore)) {
        # "Invoke-WebRequest" may not use TLS 1.2 by default, depending on the specific release of Windows 10.
        # This will be a problem if the release is downloaded from github.com since it only provides TLS 1.2.
        # So enable TLS 1.2 in "[System.Net.ServicePointManager]::SecurityProtocol", which enables it (in the current PS session)
        # for "Invoke-WebRequest" and everything else that uses "System.Net.HttpWebRequest"
        #
        # This is not needed on IoT Core since its "Invoke-WebRequest" supports TLS 1.2 by default. It *can't* be done
        # for IoT Core anyway because the "System.Net.ServicePointManager" type doesn't exist in its version of dotnet.
        [System.Net.ServicePointManager]::SecurityProtocol =
            [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
    }

    if (($ContainerOs -eq 'Windows') -and (-not $SkipBatteryCheck)) {
        if ((Get-Command 'Get-WmiObject' -ErrorAction Ignore) -ne $null) {
            [psobject[]] $batteries = Get-WmiObject Win32_Battery -ErrorAction Ignore
            if (($batteries -ne $null) -and ($batteries.Length -gt 0)) {
                Write-Warning (
                    "One or more batteries were detected on this device.`n`n" +
                    'A known Windows operating system issue prevents transition to sleep and hibernate power states when IoT Edge modules ' +
                    "(process-isolated Windows Nano Server containers) are running. This issue impacts battery life on the device.`n`n" +
                    'As a workaround, use the command "Stop-Service iotedge, iotedge-moby -Force" to stop any running IoT Edge modules ' +
                    'before using these power states.')

                if (-not $PSCmdlet.ShouldContinue('Do you want to continue with installation?', '')) {
                    Write-HostRed
                    Write-HostRed 'Aborting installation.'
                    throw
                }
            }
        }
    }
}

function Write-LogInformation {
    Write-HostGreen
    Write-HostGreen 'This device is now provisioned with the IoT Edge runtime.'
    Write-HostGreen 'Check the status of the IoT Edge service with "Get-Service iotedge"'
    Write-HostGreen 'List running modules with "iotedge list"'
    Write-HostGreen 'Display logs from the last five minutes in chronological order with "Get-IoTEdgeLog"'
}

function Test-IsDockerRunning {
    switch ($ContainerOs) {
        'Linux' {
            $service = Get-Service $DockerServiceName -ErrorAction SilentlyContinue
            if (($service -eq $null) -or ($service.Status -ne 'Running')) {
                Write-HostRed 'Docker is not running.'
                if (Test-IotCore) {
                    Write-HostRed ('Please visit https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-core ' +
                        'for assistance with installing Docker on IoT Core.')
                }
                else {
                    Write-HostRed ('You can use Docker for Windows for development and testing. ' +
                        'Please visit https://www.docker.com/docker-windows for additional information.')
                }

                return $false
            }

            Write-HostGreen 'Docker is running.'

            return $true
        }

        'Windows' {
            $service = Get-Service $MobyServiceName -ErrorAction SilentlyContinue
            if (($service -eq $null) -or ($service.Status -ne 'Running')) {
                return $false
            }

            return $true
        }
    }
}

function Set-ContainerOs {
    switch ($ContainerOs) {
        'Linux' {
            if ((Get-ExternalDockerServerOs) -ne 'Linux') {
                Write-Host 'Switching Docker to use Linux containers...'

                $dockerCliExe = "$ProgramFilesDirectory\Docker\Docker\DockerCli.exe"

                if (-not (Test-Path -Path $dockerCliExe)) {
                    Write-HostRed
                    Write-HostRed "Unable to switch to Linux containers: could not find $dockerCliExe"
                    throw
                }

                Invoke-Native """$dockerCliExe"" -SwitchDaemon"

                $newExternalDockerServerOs = Get-ExternalDockerServerOs
                if ($newExternalDockerServerOs -ne 'Linux') {
                    Write-HostRed
                    Write-HostRed "Unable to switch to Linux containers: Docker is still set to use $newExternalDockerServerOs containers"
                    throw
                }

                Write-HostGreen 'Switched Docker to use Linux containers.'
            }
        }

        'Windows' {
            # No need to test for Linux/switch to Windows containers because our
            # moby installation doesn't support Linux containers
        }
    }
}

function Get-WindowsBuild {
    return (Get-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion').'CurrentBuild'
}

function Test-EdgeAlreadyInstalled {
    return (Get-Service $EdgeServiceName -ErrorAction SilentlyContinue) -or
        (Test-Path -Path "$EdgeInstallDirectory\iotedged.exe") -or
        (Test-path -Path "$LegacyEdgeInstallDirectory\iotedged.exe")
}

function Test-MobyAlreadyInstalled {
    return (Get-Service $MobyServiceName -ErrorAction SilentlyContinue) -or
        (Test-Path -Path $MobyInstallDirectory)
}

function Test-MobyNeedsToBeMoved {
    if ($LegacyMobyStaticInstallDirectory -ne $LegacyMobyInstallDirectory) {
        return $(Test-Path -Path $LegacyMobyStaticInstallDirectory)
    }
    else {
        return $false
    }
}

function Test-LegacyInstaller {
    $legacyMobyData = (Test-Path -Path $LegacyMobyDataRootDirectory) -or
        (Test-Path -Path $LegacyMobyStaticDataRootDirectory)
    if ($legacyMobyData) {
        return $true
    }

    $newPackage = Get-Package $EdgePackage
    return -not ([bool] $newPackage)
}

function Test-AgentRegistryArgs {
    $noImageNoCreds = (-not ($AgentImage -or $Username -or $Password))
    $imageNoCreds = ($AgentImage -and -not ($Username -or $Password))
    $imageFullCreds = ($AgentImage -and $Username -and $Password)

    $valid = ($noImageNoCreds -or $imageNoCreds -or $imageFullCreds)
    if (-not $valid) {
        $message =
            if (-not $AgentImage) {
                'Parameter ''AgentImage'' is required when parameters ''Username'' and ''Password'' are specified.'
            }
            else {
                'Parameters ''Username'' and ''Password'' must be used together. Please specify both (or neither for anonymous access).'
            }
        Write-HostRed $message
    }
    return $valid
}

function Get-ContainerOs {
    $yamlPath = (Join-Path -Path $EdgeDataDirectory -ChildPath 'config.yaml')
    if (-not (Test-Path $yamlPath)) {
        return 'Windows'
    }
    $configurationYaml = Get-Content $yamlPath -Raw
    if (-not ($configurationYaml -match 'moby_runtime:\s*uri:\s*''([^'']+)''')) {
        return 'Windows'
    }

    if ($Matches[1] -eq $MobyLinuxNamedPipeUrl) {
        return 'Linux'
    }

    return 'Windows'
}

function Get-ExternalDockerServerOs {
    $dockerExe = Get-DockerCommandPrefix
    if ((Invoke-Native "$dockerExe version --format ""{{.Server.Os}}""" -Passthru) -match '\s*windows\s*$') {
        return 'Windows'
    }
    else {
        return 'Linux'
    }
}

function Install-Package([string] $Path, [ref] $RestartNeeded) {
    if (Test-IotCore) {
        Invoke-Native "ApplyUpdate -stage $Path"
    }
    else {
        $result = Add-WindowsPackage -Online -PackagePath $Path -NoRestart
        if ($result.RestartNeeded) {
            $RestartNeeded.Value = $true
        }
    }
}

function Uninstall-Package([string] $Name, [ref] $RestartNeeded) {
    if (Test-IotCore) {
        return
    }
    Get-WindowsPackage -Online |
        Where-Object { $_.PackageName -like "$Name~*"} |
        Remove-WindowsPackage -Online -NoRestart |
        ForEach-Object {
            if ($_.RestartNeeded) {
                $RestartNeeded.Value = $true
            }
        }
}

function Get-Package([string] $Name) {
    if (Test-IotCore) {
        return Invoke-Native 'ApplyUpdate -getinstalledpackages' -Passthru |
            Where-Object { $_ -like "*INFO: $Name,*"}
    }
    else {
        return Get-WindowsPackage -Online |
            Where-Object { $_.PackageName -like "$Name~*"}
    }
}

function Try-StopService([string] $Name) {
    if (Get-Service $Name -ErrorAction SilentlyContinue) {
        Stop-Service -NoWait -ErrorAction SilentlyContinue -ErrorVariable cmdErr $Name
        if ($?) {
            Start-Sleep -Seconds 7
        }
    }
}

function Get-IoTEdge([ref] $RestartNeeded, [bool] $Update) {
    try {
        # If we create the archive ourselves, then delete it when we're done
        $deleteEdgeArchive = $false

        if (Test-IotCore) {
            Invoke-Native 'ApplyUpdate -clear'
        }

        $edgeArchivePath = Download-File `
            -Description 'IoT Edge' `
            -Url 'https://aka.ms/iotedged-windows-latest-cab' `
            -DownloadFilename 'microsoft-azure-iotedge.cab' `
            -LocalCacheGlob 'microsoft-azure-iotedge.cab' `
            -Delete ([ref] $deleteEdgeArchive)
        Try-StopService $MobyServiceName
        Try-StopService $EdgeServiceName
        Install-Package -Path $edgeArchivePath -RestartNeeded $RestartNeeded
        if (-not $Update) {
            Stop-Service $EdgeServiceName -ErrorAction SilentlyContinue

            foreach ($name in 'mgmt', 'workload') {
                # We can't bind socket files directly in Windows, so create a folder
                # and bind to that. The folder needs to give Modify rights to a
                # well-known group that will exist in any container so that
                # non-privileged modules can access it.
                $path = "$EdgeDataDirectory\$name"
                New-Item $Path -ItemType Directory -Force | Out-Null
                $sid = New-Object System.Security.Principal.SecurityIdentifier 'S-1-5-11' # NT AUTHORITY\Authenticated Users
                $rule = New-Object -TypeName System.Security.AccessControl.FileSystemAccessRule(`
                    $sid, 'Modify', 'ObjectInherit', 'InheritOnly', 'Allow')
                $acl = Get-Acl -Path $path
                $acl.AddAccessRule($rule)
                Set-Acl -Path $path -AclObject $acl
            }
        }
    }
    finally {
        if ($deleteEdgeArchive) {
            Remove-Item $edgeArchivePath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (Test-IotCore) {
        Write-Host ('Committing changes, this will cause a reboot on success. ' +
            'If this is the first time installation, run "Initialize-IoTEdge" after the reboot completes.')
        $output = Invoke-Native 'ApplyUpdate -commit' -DoNotThrow -Passthru
        # On success, this should reboot, we currently cannot block that
        if ($LASTEXITCODE -ne 0) {
            Write-HostRed
            Write-HostRed "Failed to deploy, consider rebooting. Please refer to the following for more information:"
            Write-HostRed "$output"
            throw
        }
        Start-Sleep -Seconds 120
        $output = Invoke-Native 'ApplyUpdate -status' -DoNotThrow -Passthru
        Write-HostRed
        Write-HostRed "Failed to deploy. Please refer to the following for more information:"
        Write-HostRed "$output"
        throw
    }
}

Function Remove-SecurityDaemonDirectory([string] $Path)
{
    Write-Host "Deleting data directory '$Path'..."
    Remove-Item -Recurse $Path -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    if ($?) {
        Write-Verbose "Deleted data directory '$Path'"
    }
    elseif ($cmdErr.FullyQualifiedErrorId -ne 'PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand') {
        Write-Verbose "$cmdErr"
        Write-HostRed ("Could not delete directory '$Path'. Please reboot " +
            'your device and run "Uninstall-IoTEdge" again with "-Force".')
    }
    else {
        Write-Verbose "$cmdErr"
    }
}

function Delete-Directory([string] $Path) {
    if (-not (Test-Path $Path)) {
        return
    }

    # Removing "$MobyDataRootDirectory" is tricky. Windows base images contain files owned by TrustedInstaller, etc
    # Deleting them is a three-step process:
    #
    # 1. Take ownership of all files
    Invoke-Native "takeown /r /skipsl /d y /f ""$Path"""

    # 2. Reset their ACLs so that they inherit from their container
    Invoke-Native "icacls ""$Path"" /reset /t /l /q /c"

    # 3. Use cmd's "rd" rather than "Remove-Item" since the latter gets tripped up by reparse points, etc.
    #    Prepend the path with "\\?\" since the layer directories have long names, so the paths usually exceed 260 characters,
    #    and IoT Core's filesystem doesn't seem to automatically use (or even have) short names
    Invoke-Native "rd /s /q ""\\?\$Path"""
}

function Remove-IoTEdgeResources([bool] $LegacyInstaller) {
    $success = $true

    if ($LegacyEdgeInstallDirectory -ne $EdgeDataDirectory) {
        Remove-SecurityDaemonDirectory $LegacyEdgeInstallDirectory
    }
    Remove-SecurityDaemonDirectory $EdgeDataDirectory

    if (Test-Path $LegacyEdgeEventLogInstallDirectory) {
        Remove-Item -Recurse $LegacyEdgeEventLogInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr
        if ($?) {
            Write-Verbose "Deleted install directory '$LegacyEdgeEventLogInstallDirectory'"
        }
        elseif ($cmdErr.FullyQualifiedErrorId -ne 'PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand') {
            Write-Verbose "$cmdErr"
            Write-Warning "Could not delete '$LegacyEdgeEventLogInstallDirectory'."
            Write-Warning 'If you are reinstalling or updating IoT Edge, then this is safe to ignore.'
            Write-Warning ('Otherwise, please close Event Viewer, or any PowerShell windows where you ran Get-WinEvent, ' +
                'then run "Uninstall-IoTEdge" again with "-Force".')
        }
        else {
            Write-Verbose "$cmdErr"
        }
    }

    if ($LegacyInstaller) {
        # Check whether we need to clean up after an errant installation into the OS partition on IoT Core
        if ($env:ProgramData -ne 'C:\ProgramData') {
            Write-Verbose 'Multiple ProgramData directories found'
            $existingMobyDataRoots = $LegacyMobyDataRootDirectory, $LegacyMobyStaticDataRootDirectory
            $existingMobyInstallations = $LegacyMobyInstallDirectory, $LegacyMobyStaticInstallDirectory
        }
        else {
            $existingMobyDataRoots = $LegacyMobyDataRootDirectory
            $existingMobyInstallations = $LegacyMobyInstallDirectory
        }
    }
    else {
        $existingMobyDataRoots = $MobyDataRootDirectory
        $existingMobyInstallations = @()
    }

    foreach ($root in $existingMobyDataRoots | ?{ Test-Path $_ }) {
        try {
            Write-Host "Deleting Moby data root directory '$root'..."
            Delete-Directory $root
            Write-Verbose "Deleted Moby data root directory '$root'"
        }
        catch {
            Write-Verbose "$_"
            Write-HostRed ("Could not delete Moby data root directory '$root'. Please reboot " +
                'your device and run "Uninstall-IoTEdge" again with "-Force".')
            $success = $false
        }
    }

    foreach ($install in $existingMobyInstallations | ?{ Test-Path $_ }) {
        try {
            Write-Host "Deleting directory '$install'..."
            Delete-Directory $install
            Write-Verbose "Deleted directory '$install'"
        }
        catch {
            Write-Verbose $_
            Write-HostRed ("Could not delete directory '$install'. Please reboot " +
                'your device and run "Uninstall-IoTEdge" again with "-Force".')
            $success = $false
        }
    }

    return $success
}

function Get-MachineEnvironmentVariable([string] $Name) {
    # Equivalent to "[System.Environment]::GetEnvironmentVariable($Name, [System.EnvironmentVariableTarget]::Machine)"
    # but IoT Core doesn't have this overload

    return (Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment').$Name
}

function Set-MachineEnvironmentVariable([string] $Name, [string] $Value) {
    # Equivalent to "[System.Environment]::SetEnvironmentVariable($Name, $Value, [System.EnvironmentVariableTarget]::Machine)"
    # but IoT Core doesn't have this overload; however, the direct registry route requires a reboot to make it available everywhere

    if (Test-IotCore) {
        Set-ItemProperty `
            -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' `
            -Name $Name `
            -Value $Value
    }
    else {
        [System.Environment]::SetEnvironmentVariable($Name, $Value, [System.EnvironmentVariableTarget]::Machine)
    }
}

function Remove-MachineEnvironmentVariable([string] $Name) {
    # Equivalent to "[System.Environment]::SetEnvironmentVariable($Name, $null, [System.EnvironmentVariableTarget]::Machine)"
    # but IoT Core doesn't have this overload

    Remove-ItemProperty `
        -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' `
        -Name $Name `
        -ErrorAction SilentlyContinue
}

function Get-SystemPath {
    return ((Get-MachineEnvironmentVariable 'Path') -split ';' | Where-Object { $_.Length -gt 0 })
}

function Set-SystemPath {
    $systemPath = Get-SystemPath

    $needsModification = ($systemPath -notcontains $EdgeInstallDirectory) -or
        (($ContainerOs -eq 'Windows') -and ($systemPath -notcontains $MobyInstallDirectory))
    if (-not $needsModification) {
        Write-HostGreen 'System PATH does not require an update.'
        return
    }

    if ($systemPath -notcontains $EdgeInstallDirectory) {
        $systemPath += $EdgeInstallDirectory
    }

    if (($ContainerOs -eq 'Windows') -and ($systemPath -notcontains $MobyInstallDirectory)) {
        $systemPath += $MobyInstallDirectory
    }

    Set-MachineEnvironmentVariable 'PATH' ($systemPath -join ';')
    $env:PATH += ";$EdgeInstallDirectory;$MobyInstallDirectory"

    Write-HostGreen 'Updated system PATH.'
}

function Reset-SystemPath {
    $systemPath = Get-SystemPath

    $needsModification =
        ($systemPath -contains $EdgeInstallDirectory) -or
        ($systemPath -contains $MobyInstallDirectory) -or
        ($systemPath -contains $LegacyMobyStaticInstallDirectory) -or
        ($systemPath -contains $LegacyEdgeInstallDirectory) -or
        ($systemPath -contains $LegacyMobyInstallDirectory)
    if (-not $needsModification) {
        return
    }

    $systemPath = $systemPath | Where-Object {
        return ($_ -ne $EdgeInstallDirectory) -and
            ($_ -ne $MobyInstallDirectory) -and
            ($_ -ne $LegacyMobyStaticInstallDirectory) -and
            ($_ -ne $LegacyEdgeInstallDirectory) -and
            ($_ -ne $LegacyMobyInstallDirectory)
    }
    Set-MachineEnvironmentVariable 'PATH' ($systemPath -join ';')

    Write-Verbose 'Removed IoT Edge directories from system PATH'
}

function Get-VcRuntime {
    if (Test-IotCore) {
        Write-HostGreen 'Skipping VC Runtime installation on IoT Core.'
        return
    }

    if (Test-Path 'C:\Windows\System32\vcruntime140.dll') {
        Write-HostGreen 'Skipping VC Runtime installation because it is already installed.'
        return
    }

    $deleteVcRuntimeArchive = $false

    $vcRuntimeArchivePath = Download-File `
        -Description 'VC Runtime installer' `
        -Url 'https://download.microsoft.com/download/0/6/4/064F84EA-D1DB-4EAA-9A5C-CC2F0FF6A638/vc_redist.x64.exe' `
        -DownloadFilename 'vc_redist.x64.exe' `
        -LocalCacheGlob '*vc_redist*.exe' `
        -Delete ([ref] $deleteVcRuntimeArchive)
    try {
        Invoke-Native """$vcRuntimeArchivePath"" /quiet /norestart"
        Write-HostGreen 'Installed VC Runtime.'
    }
    catch {
        if ((Test-Path Variable:\LASTEXITCODE) -and ($LASTEXITCODE -eq 1638)) {
            Write-HostGreen 'Skipping VC Runtime installation because a newer version is already installed.'
        }
        else {
            throw $_
        }
    }
    finally {
        if ($deleteVcRuntimeArchive) {
            Remove-Item $vcRuntimeArchivePath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Start-IoTEdgeService([bool] $RestartNeeded = $false) {
    if (-not $RestartNeeded) {
        Start-Service $EdgeServiceName
    }

    Write-HostGreen 'Initialized the IoT Edge service.'
}

function Uninstall-Services([ref] $RestartNeeded, [bool] $LegacyInstaller) {
    if (Get-Service $EdgeServiceName -ErrorAction SilentlyContinue) {
        Set-Service -StartupType Disabled $EdgeServiceName -ErrorAction SilentlyContinue
        Stop-Service -NoWait -ErrorAction SilentlyContinue -ErrorVariable cmdErr $EdgeServiceName
        if ($?) {
            Start-Sleep -Seconds 7
            Write-Verbose 'Stopped the IoT Edge service'
        }
        else {
            Write-Verbose "$cmdErr"
        }
    }

    Remove-IotEdgeContainers

    if (Get-Service $MobyServiceName -ErrorAction SilentlyContinue) {
        Stop-Service -NoWait -ErrorAction SilentlyContinue -ErrorVariable cmdErr $MobyServiceName
        if ($?) {
            Start-Sleep -Seconds 7
            Write-Verbose 'Stopped the IoT Edge Moby Engine service'
        }
        else {
            Write-Verbose "$cmdErr"
        }
    }

    if ($LegacyInstaller) {
        if (Get-Service $EdgeServiceName -ErrorAction SilentlyContinue) {
            if (Invoke-Native "sc.exe delete ""$EdgeServiceName""" -ErrorAction SilentlyContinue) {
                Write-Verbose 'Removed IoT Edge service subkey from the registry'
            }
        }
        if (Get-Service $MobyServiceName -ErrorAction SilentlyContinue) {
            if (Invoke-Native "sc.exe delete ""$MobyServiceName""" -ErrorAction SilentlyContinue) {
                Write-Verbose 'Removed IoT Edge Moby Engine service subkey from the registry'
            }
        }
    }
    else {
        Uninstall-Package -Name $EdgePackage -RestartNeeded $RestartNeeded
    }
}

function Add-FirewallExceptions {
    New-NetFirewallRule `
        -DisplayName 'iotedged allow inbound 15580,15581' `
        -Direction 'Inbound' `
        -Action 'Allow' `
        -Protocol 'TCP' `
        -LocalPort '15580-15581' `
        -Program "$EdgeInstallDirectory\iotedged.exe" `
        -InterfaceType 'Any' | Out-Null
    Write-HostGreen 'Added firewall exceptions for ports used by the IoT Edge service.'
}

function Remove-FirewallExceptions {
    Remove-NetFirewallRule -DisplayName 'iotedged allow inbound 15580,15581' -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    Write-Verbose "$(if ($?) { 'Removed firewall exceptions' } else { $cmdErr })"
}

function Update-ConfigYaml([ScriptBlock] $UpdateFunc)
{
    $yamlPath = (Join-Path -Path $EdgeDataDirectory -ChildPath 'config.yaml')
    $configurationYaml = Get-Content $yamlPath -Raw
    $configurationYaml = $UpdateFunc.Invoke($configurationYaml)
    $configurationYaml | Set-Content $yamlPath -Force
}

function Validate-GatewaySettings {
    $certFilesProvided = $false
    if ($DeviceCACertificate -or $DeviceCAPrivateKey -or $DeviceTrustbundle) {
        if (-Not (Test-Path -Path $DeviceCACertificate)) {
            Write-HostRed
            Write-HostRed "Device CA certificate file $DeviceCACertificate not found. When configuring device certificates, a certificate file is required."
            throw
        }
        if (-Not (Test-Path -Path $DeviceCAPrivateKey)) {
            Write-HostRed
            Write-HostRed "Device CA private key file $DeviceCAPrivateKey not found. When configuring device certificates, a private key file is required."
            throw
        }
        if (-Not (Test-Path -Path $DeviceTrustbundle)) {
            Write-HostRed
            Write-HostRed "Device trustbundle file $DeviceTrustbundle not found. When configuring device certificates, a trust bundle file is required."
            throw
        }
        $certFilesProvided = $true
    }

    return $certFilesProvided
}

function Get-DpsProvisioningSettings {
    $idCertFilesProvided = $false
    $attestationMethod = 'tpm' # default
    if ($SymmetricKey) {
        $attestationMethod = 'symmetric_key'
    }
    elseif ($AutoGenX509IdentityCertificate) {
        $attestationMethod = 'x509'
    }
    elseif ($X509IdentityCertificate -or $X509IdentityPrivateKey) {
        $attestationMethod = 'x509'
        $idCertFilesProvided = $true
    }

    if ($idCertFilesProvided) {
        if ($RegistrationId) {
            Write-HostYellow 'NOTE: RegistrationId is strictly not required for this DPS provisioning mode as it can be obtained from the identity certificate'
        }
    }
    else {
        if (-not $RegistrationId) {
            Write-HostRed
            Write-HostRed "RegistrationId is required for this DPS provisioning mode."
            throw
        }
    }

    if ($attestationMethod -eq 'x509') {
        if ($idCertFilesProvided) {
            if (-Not (Test-Path -Path $X509IdentityCertificate)) {
                Write-HostRed
                Write-HostRed "Identity certificate file $X509IdentityCertificate not found."
                throw
            }
            if (-Not (Test-Path -Path $X509IdentityPrivateKey)) {
                Write-HostRed
                Write-HostRed "Identity private file $X509IdentityPrivateKey not found."
                throw
            }
        }
        else {
            if ($X509IdentityCertificate -or $X509IdentityPrivateKey) {
                Write-HostRed
                Write-HostRed 'Cannot specify a device identity certificate and also set AutoGenX509IdentityCertificate as true.'
                throw
            }
            if (-Not (Validate-GatewaySettings)) {
                Write-HostRed
                Write-HostRed 'Device CA certificate files are not found. These are required when using AutoGenX509IdentityCertificate.'
                throw
            }
        }
    }

    return $attestationMethod
}

function Set-ProvisioningMode {
    Update-ConfigYaml({
        param($configurationYaml)

        if ($Manual -or $DeviceConnectionString) {
            $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*device_connection_string:\s*".*"'
            $replacementContent = @(
                'provisioning:',
                '  source: ''manual''',
                "  device_connection_string: '$DeviceConnectionString'")
            $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))
            Write-HostGreen 'Configured device for manual provisioning.'
            return $configurationYaml
        }
        elseif ($External -or $ExternalProvisioningEndpoint){
            $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*endpoint:\s*".*"'
            $replacementContent = @(
                'provisioning:',
                '  source: ''external''',
                "  endpoint: '$ExternalProvisioningEndpoint'")
            $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))
            Write-HostGreen 'Configured device for external provisioning.'
            return $configurationYaml
        }
        else {
            $attestationMethod = Get-DpsProvisioningSettings
            $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*global_endpoint:\s*".*"\s*#?\s*scope_id:\s*".*"\s*#?\s*attestation:\s*#?\s*method:\s*"' + $attestationMethod + '"\s*#?\s*registration_id:\s*".*"\s*#?\s*device_id:\s*".*"'

            if ($attestationMethod -eq 'symmetric_key') {
                $selectionRegex += '\s*#?\s*symmetric_key:\s".*"'
            } elseif ($attestationMethod -eq 'x509') {
                $selectionRegex += '\s*#?\s*identity_cert:\s".*"\s*#?\s*identity_pk:\s".*"'
            }
            $replacementContent = @(
                'provisioning:',
                '  source: ''dps''',
                '  global_endpoint: ''https://global.azure-devices-provisioning.net''',
                "  scope_id: '$ScopeId'",
                "  attestation:",
                "    method: '$attestationMethod'")
            if ($RegistrationId) {
                $replacementContent += "    registration_id: '$RegistrationId'"
            }
            if ($SymmetricKey) {
                $replacementContent += "    symmetric_key: '$SymmetricKey'"
            }
            if ($X509IdentityCertificate) {
                $replacementContent += "    identity_cert: '$X509IdentityCertificate'"
            }
            if ($X509IdentityPrivateKey) {
                $replacementContent += "    identity_pk: '$X509IdentityPrivateKey'"
            }
            $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")

            $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*device_connection_string:\s*".*"'
            $replacementContent = ''
            $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))

            Write-HostGreen 'Configured device for DPS provisioning.'
            return $configurationYaml
        }
    })
}

function Set-Certificates {
    if (Validate-GatewaySettings) {
        Update-ConfigYaml({
            param($configurationYaml)
            $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?certificates:\s*#?\s*device_ca_cert:\s*".*"\s*#?\s*device_ca_pk:\s*".*"\s*#?\s*trusted_ca_certs:\s*".*"'
            $replacementContent = @(
                "certificates:",
                "  device_ca_cert: '$DeviceCACertificate'",
                "  device_ca_pk: '$DeviceCAPrivateKey'",
                "  trusted_ca_certs: '$DeviceTrustbundle'")
            $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))
            Write-HostGreen 'Configured device for manual provisioning.'
            return $configurationYaml
        })
    }
}

function Set-AgentImage {
    if ($AgentImage) {
        Update-ConfigYaml({
            param($configurationYaml)
            $selectionRegex = 'image:\s*".*"'
            $replacementContent = "image: '$AgentImage'"
            $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")
            if ($Username -and $Password) {
                $selectionRegex = '\n    auth:\s*\{\s*\}'
                $agentRegistry = Get-AgentRegistry
                $cred = New-Object System.Management.Automation.PSCredential ($Username, $Password)
                $replacementContent = @(
                    '',
                    '    auth:',
                    "      serveraddress: '$agentRegistry'",
                    "      username: '$Username'",
                    "      password: '$($cred.GetNetworkCredential().Password)'")
                $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))
            }
            Write-HostGreen "Configured device with agent image '$AgentImage'."
            return $configurationYaml
        })
    }
}

function Set-Hostname {
    Update-ConfigYaml({
        param($configurationYaml)

        $hostname = [System.Net.Dns]::GetHostName()
        $selectionRegex = 'hostname:\s*".*"'
        $replacementContent = "hostname: '$hostname'"
        $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))
        Write-HostGreen "Configured device with hostname '$hostname'."
        return $configurationYaml
    })
}

function Set-ConfigUri([string] $Section, [string] $ManagementUri, [string] $WorkloadUri) {
    Update-ConfigYaml({
        param($configurationYaml)

        $selectionRegex = ('{0}:\s*management_uri:\s*".*"\s*workload_uri:\s*".*"' -f $Section)
        $replacementContent = @(
            ('{0}:' -f $Section),
            "  management_uri: '$ManagementUri'",
            "  workload_uri: '$WorkloadUri'")
        $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")
        return $configurationYaml
    })
}

function Set-ListenConnectUri([string] $ManagementUri, [string] $WorkloadUri) {
    Set-ConfigUri -Section 'connect' -ManagementUri $ManagementUri -WorkloadUri $WorkloadUri
    Set-ConfigUri -Section 'listen' -ManagementUri $ManagementUri -WorkloadUri $WorkloadUri

    Set-MachineEnvironmentVariable 'IOTEDGE_HOST' $ManagementUri
    $env:IOTEDGE_HOST = $ManagementUri
}

function Set-GatewayAddress {
    $gatewayAddress = (Get-NetIpAddress |
            Where-Object {$_.InterfaceAlias -like '*vEthernet (DockerNAT)*' -and $_.AddressFamily -eq 'IPv4'}).IPAddress

    Set-ListenConnectUri `
        -ManagementUri "http://${gatewayAddress}:15580" `
        -WorkloadUri "http://${gatewayAddress}:15581"

    Write-HostGreen "Configured device with gateway address '$gatewayAddress'."
}

function Set-CorrectProgramData {
    $forwardProgramData = $env:ProgramData -replace '\\', '/'

    Set-ListenConnectUri `
        -ManagementUri ('unix:///{0}/iotedge/mgmt/sock' -f $forwardProgramData) `
        -WorkloadUri ('unix:///{0}/iotedge/workload/sock' -f $forwardProgramData)

    Update-ConfigYaml({
        param($configurationYaml)

        $selectionRegex = 'homedir:\s".*"'
        $replacementContent = ('homedir: "{0}"' -f ($EdgeDataDirectory -replace '\\', '\\'))
        $configurationYaml = $configurationYaml -replace $selectionRegex, $replacementContent

        Write-HostGreen 'Configured ProgramData directory.'
        return $configurationYaml
    })
}

function Set-MobyEngineParameters {
    Update-ConfigYaml({
        param($configurationYaml)

        $selectionRegex = 'moby_runtime:\s*uri:\s*".*"\s*#?\s*network:\s*".*"'
        $mobyUrl = switch ($ContainerOs) {
            'Linux' {
                $MobyLinuxNamedPipeUrl
            }

            'Windows' {
                $MobyNamedPipeUrl
            }
        }
        $mobyNetwork = switch ($ContainerOs) {
            'Linux' {
                'azure-iot-edge'
            }

            'Windows' {
                'nat'
            }
        }
        $replacementContent = @(
            'moby_runtime:',
            "  uri: '$mobyUrl'",
            "  network: '$mobyNetwork'")
        $configurationYaml = ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n"))
        Write-HostGreen "Configured device with Moby Engine URL '$mobyUrl' and network '$mobyNetwork'."
        return $configurationYaml
    })
}

function Get-AgentRegistry {
    $parts = $AgentImage -split '/'
    if (($parts.Length -gt 1) -and ($parts[0] -match '\.')) {
        return $parts[0]
    }
    return 'index.docker.io'
}

function Stop-EdgeContainer([string] $Name = '') {
    $dockerExe = Get-DockerCommandPrefix
    $allContainersString = Invoke-Native "$dockerExe ps --all --format ""{{.ID}}""" -Passthru
    [string[]] $allContainers = $allContainersString -split {$_ -eq "`r" -or $_ -eq "`n"} | where {$_.Length -gt 0}

    foreach ($containerId in $allContainers) {
        $inspectString = Invoke-Native "$dockerExe inspect ""$containerId""" -Passthru
        $inspectResult = ($inspectString | ConvertFrom-Json)[0]

        if ($Name -and ($inspectResult.Name -ne $Name)) {
            continue
        }

        $label = $inspectResult.Config.Labels | Get-Member -MemberType NoteProperty -Name 'net.azure-devices.edge.owner' | %{ $inspectResult.Config.Labels | Select-Object -ExpandProperty $_.Name }

        if (($label -eq 'Microsoft.Azure.Devices.Edge.Agent')) {
            if (($inspectResult.Name -eq '/edgeAgent') -or ($inspectResult.Name -eq '/edgeHub')) {
                Invoke-Native "$dockerExe rm --force ""$containerId""" -Backoff
                Write-Verbose "Stopped and deleted container $($inspectResult.Name)"

                # ".Config.Image" contains the user-provided name, but this can be a tag like "foo:latest"
                # that doesn't necessarily point to the image that has that tag right now.
                #
                # So delete the image using its unique identifier, as given by ".Image", like "sha256:1234abcd..."
                Invoke-Native "$dockerExe image rm --force ""$($inspectResult.Image)""" -Backoff
                Write-Verbose "Deleted image of container $($inspectResult.Name) ($($inspectResult.Config.Image))"
            }
            else {
                Invoke-Native "$dockerExe stop ""$containerId"""
                Write-Verbose "Stopped container $($inspectResult.Name)"
            }
        }
    }
}

function Remove-IotEdgeContainers {
    if (-not (Test-IsDockerRunning 6> $null)) {
        return
    }

    # Need to stop Agent first so it does not restart other containers
    Stop-EdgeContainer -Name '/edgeAgent'
    Stop-EdgeContainer

    if ($ContainerOs -eq "Windows") {
        try {
            $dockerExe = Get-DockerCommandPrefix
            Invoke-Native "$dockerExe container prune -f"
            Invoke-Native "$dockerExe image prune -f -a"
            Invoke-Native "$dockerExe system prune -f -a"
        }
        catch {
            # Best effort
        }
    }
}

function Get-DockerCommandPrefix {
    switch ($ContainerOs) {
        'Linux' {
            return '"docker"'
        }

        'Windows' {
            # docker needs two more slashes after the scheme
            $namedPipeUrl = $MobyNamedPipeUrl -replace 'npipe://\./pipe/', 'npipe:////./pipe/'
            $prefix = ""
            # in case the installation has not been completed
            if (-not (Get-Command "docker.exe" -ErrorAction SilentlyContinue)) {
                $prefix = "$MobyInstallDirectory\"
            }
            return ('"{0}docker" -H "{1}"' -f $prefix, $namedPipeUrl)
        }
    }
}

function Invoke-Native {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [String] $Command,

        [Switch] $Passthru,

        [Switch] $DoNotThrow,

        [Switch] $Backoff
    )

    process {
        Write-Verbose "Executing native Windows command '$Command'..."
        $sleep = 1
        for ($i = 0; $i -lt 5; $i++) {
            if ($i -ne 0) {
                Start-Sleep -Seconds $sleep
                $sleep *= 2
                Write-Verbose 'Retrying...'
            }

            $out = cmd /c "($Command) 2>&1" 2>&1 | Out-String
            Write-Verbose $out
            Write-Verbose "Exit code: $LASTEXITCODE"

            if (($LASTEXITCODE -eq 0) -or (-not $Backoff)) {
                break
            }
        }

        if (($LASTEXITCODE -ne 0) -and (-not $DoNotThrow)) {
            throw ("Failed to execute '{0}': `n{1}" -f $Command, $out)
        }
        elseif ($Passthru) {
            return $out
        }
    }
}

function Test-IotCore {
    (Get-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion').'EditionID' -eq 'IoTUAP'
}

function Write-HostGreen {
    Write-Host -ForegroundColor Green @args
}

function Write-HostRed {
    Write-Host -ForegroundColor Red @args
}

function Write-HostYellow {
    Write-Host -ForegroundColor Yellow @args
}

function Remove-BuiltinWritePermissions([string] $Path) {
    $sid = New-Object System.Security.Principal.SecurityIdentifier 'S-1-5-32-545' # BUILTIN\Users

    Write-Verbose  "Remove BUILTIN\Users permission to $Path"
    Invoke-Native "icacls ""$Path"" /inheritance:d"

    $acl = Get-Acl -Path $Path
    $write = [System.Security.AccessControl.FileSystemRights]::Write
    foreach ($access in $acl.Access) {
        $accessSid = $access.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier])

        if ($accessSid -eq $sid -and
            $access.AccessControlType -eq 'Allow' -and
            ($access.FileSystemRights -band $write) -eq $write)
        {
            $rule = New-Object -TypeName System.Security.AccessControl.FileSystemAccessRule(`
                $sid, 'Write', $access.InheritanceFlags, $access.PropagationFlags, 'Allow')
            $acl.RemoveAccessRule($rule) | Out-Null
        }
    }
    Set-Acl -Path $Path -AclObject $acl
}

function Download-File([string] $Description, [string] $Url, [string] $DownloadFilename, [string] $LocalCacheGlob, [ref] $Delete) {
    if (($OfflineInstallationPath -ne '') -and (Test-Path "$OfflineInstallationPath\$LocalCacheGlob")) {
        $result = (Get-Item "$OfflineInstallationPath\$LocalCacheGlob" | Select-Object -First 1).FullName

        $Delete.Value = $false
    }
    else {
        Write-Host "Downloading $Description..."

        $OldProgressPreference = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        $outFile = (Join-Path -Path $env:TEMP -ChildPath $DownloadFileName)
        try {
            Invoke-WebRequest `
                -Uri $Url `
                -OutFile $outFile `
                -UseBasicParsing `
                @InvokeWebRequestParameters
        }
        finally {
            $ProgressPreference = $OldProgressPreference
        }

        $Delete.Value = $true
        $result = $outFile
    }

    Write-HostGreen "Using $Description from $result"
    return $result
}

New-Alias -Name Install-SecurityDaemon -Value Install-IoTEdge -Force
New-Alias -Name Uninstall-SecurityDaemon -Value Uninstall-IoTEdge -Force

Export-ModuleMember `
    -Function `
        Deploy-IoTEdge,
        Initialize-IoTEdge,
        Get-IoTEdgeLog,
        Update-IoTEdge,
        Install-IoTEdge,
        Uninstall-IoTEdge `
    -Alias `
        Install-SecurityDaemon,
        Uninstall-SecurityDaemon
}
