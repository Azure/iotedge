New-Module -Name IotEdgeSecurityDaemon -ScriptBlock {

[Console]::OutputEncoding = New-Object -typename System.Text.ASCIIEncoding

<#
 # Installs the IoT Edge Security Daemon on Windows.
 #>

#requires -Version 5
#requires -RunAsAdministrator

Set-Variable Windows1607 -Value 14393 -Option Constant
Set-Variable Windows1803 -Value 17134 -Option Constant
Set-Variable Windows1809 -Value 17763 -Option Constant

Set-Variable MinBuildForLinuxContainers -Value $Windows1607
Set-Variable SupportedBuildsForWindowsContainers -Value @($Windows1809)

Set-Variable DockerServiceName -Value 'com.docker.service' -Option Constant

Set-Variable EdgeInstallDirectory -Value 'C:\ProgramData\iotedge' -Option Constant
Set-Variable EdgeEventLogName -Value 'iotedged' -Option Constant
Set-Variable EdgeEventLogInstallDirectory -Value 'C:\ProgramData\iotedge-eventlog' -Option Constant
Set-Variable EdgeServiceName -Value 'iotedge' -Option Constant

Set-Variable EventLogApplicationRegPath -Value 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application' -Option Constant

Set-Variable MobyDataRootDirectory -Value "$env:ProgramData\iotedge-moby-data" -Option Constant
Set-Variable MobyStaticDataRootDirectory -Value 'C:\ProgramData\iotedge-moby-data' -Option Constant
Set-Variable MobyInstallDirectory -Value "$env:ProgramData\iotedge-moby" -Option Constant
Set-Variable MobyStaticInstallDirectory -Value 'C:\ProgramData\iotedge-moby' -Option Constant
Set-Variable MobyLinuxNamedPipeUrl -Value 'npipe://./pipe/docker_engine' -Option Constant
Set-Variable MobyNamedPipeUrl -Value 'npipe://./pipe/iotedge_moby_engine' -Option Constant
Set-Variable MobyServiceName -Value 'iotedge-moby' -Option Constant

enum ContainerOs {
    Linux
    Windows
}

<#
.SYNOPSIS

Installs the IoT Edge Security Daemon and its dependencies.


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Install-SecurityDaemon -Manual -DeviceConnectionString $deviceConnectionString -ContainerOs Windows


.EXAMPLE

PS> Install-SecurityDaemon -Dps -ScopeId $scopeId -RegistrationId $registrationId -ContainerOs Windows


.EXAMPLE

PS> Install-SecurityDaemon -ExistingConfig -ContainerOs Windows
#>
function Install-SecurityDaemon {
    [CmdletBinding(DefaultParameterSetName = 'Manual')]
    param (
        # Specified the daemon will be configured manually, using a device connection string.
        [Parameter(ParameterSetName = 'Manual')]
        [Switch] $Manual,

        # Specified the daemon will be configured using DPS, using a scope ID and registration ID.
        [Parameter(ParameterSetName = 'DPS')]
        [Switch] $Dps,

        # Specified the daemon will be configured to use an existing config.yaml
        [Parameter(ParameterSetName = 'ExistingConfig')]
        [Switch] $ExistingConfig,

        # The device connection string.
        [Parameter(Mandatory = $true, ParameterSetName = 'Manual')]
        [String] $DeviceConnectionString,

        # The DPS scope ID.
        [Parameter(Mandatory = $true, ParameterSetName = 'DPS')]
        [String] $ScopeId,

        # The DPS registration ID.
        [Parameter(Mandatory = $true, ParameterSetName = 'DPS')]
        [String] $RegistrationId,

        # The base OS of all the containers that will be run on this device via the security daemon.
        #
        # If set to Linux, a separate installation of Docker for Windows is expected.
        #
        # If set to Windows, the Moby Engine will be installed automatically. This will not affect any existing installation of Docker for Windows.
        [ContainerOs] $ContainerOs = 'Linux',

        # Proxy URI used for all Invoke-WebRequest calls. To specify other proxy-related options like -ProxyCredential, see -InvokeWebRequestParameters
        [Uri] $Proxy,

        # If set to a directory, the installer prefers to use iotedged zip, Moby Engine zip, Moby CLI zip and VC Runtime MSI files from inside this directory
        # over downloading them from the internet. Thus placing all four files in this directory can be used to have a completely offline install,
        # or a specific subset can be placed to override the online versions of those specific components.
        [String] $OfflineInstallationPath,

        # IoT Edge Agent image to pull for the initial configuration.
        [Parameter(ParameterSetName = 'Manual')]
        [Parameter(ParameterSetName = 'DPS')]
        [String] $AgentImage,

        # Username used to access the container registry and pull the IoT Edge Agent image.
        [Parameter(ParameterSetName = 'Manual')]
        [Parameter(ParameterSetName = 'DPS')]
        [String] $Username,

        # Password used to access the container registry and pull the IoT Edge Agent image.
        [Parameter(ParameterSetName = 'Manual')]
        [Parameter(ParameterSetName = 'DPS')]
        [SecureString] $Password,

        # Splatted into every Invoke-WebRequest invocation. Can be used to set extra options.
        #
        # If -Proxy is also specified, it overrides the `-Proxy` key set in this hashtable, if any.
        #
        # Example:
        #
        #     Install-SecurityDaemon -InvokeWebRequestParameters @{ '-Proxy' = 'http://localhost:8888'; '-ProxyCredential' = (Get-Credential).GetNetworkCredential() }
        [HashTable] $InvokeWebRequestParameters,

        # Don't install the Moby CLI (docker.exe) to $MobyInstallDirectory. Only takes effect for -ContainerOs 'Windows'
        [Switch] $SkipMobyCli,

        # Local path to iotedged zip file. Only kept for backward compatibility. Prefer to set -OfflineInstallationPath instead.
        [String] $ArchivePath
    )

    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version 5

    if ($InvokeWebRequestParameters -eq $null) {
        $InvokeWebRequestParameters = @{}
    }

    if ($Proxy -ne $null) {
        $InvokeWebRequestParameters['-Proxy'] = $Proxy
    }

    if (Test-EdgeAlreadyInstalled) {
        Write-HostRed
        Write-HostRed 'IoT Edge is already installed. To reinstall, run `Uninstall-SecurityDaemon` first.'
        return
    }

    if (Test-MobyAlreadyInstalled) {
        Write-HostRed
        Write-HostRed 'IoT Edge Moby Engine is already installed. To reinstall, run `Uninstall-SecurityDaemon` first.'
        return
    }
    
    if (Test-MobyNeedsToBeMoved) {
        Write-HostRed
        Write-HostRed 'IoT Edge Moby Engine is installed in an invalid location. To reinstall, run `Uninstall-SecurityDaemon -DeleteMobyDataRoot` first.'
        return
    }

    if (-not (Test-AgentRegistryArgs)) {
        return
    }

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
    if ($preRequisitesMet) {
        Write-HostGreen "The container host is on supported build version $currentWindowsBuild."
        Set-ContainerOs
    }
    else {
        Write-HostRed
        Write-HostRed ('The prerequisites for installation of the IoT Edge Security daemon are not met. ' +
            'Please fix all known issues before rerunning this script.')
        return
    }

    if (-not (Test-IotCore)) {
        # `Invoke-WebRequest` may not use TLS 1.2 by default, depending on the specific release of Windows 10.
        # This will be a problem if the release is downloaded from github.com since it only provides TLS 1.2.
        # So enable TLS 1.2 in `[System.Net.ServicePointManager]::SecurityProtocol`, which enables it (in the current PS session)
        # for `Invoke-WebRequest` and everything else that uses `System.Net.HttpWebRequest`
        #
        # This is not needed on IoT Core since its `Invoke-WebRequest` supports TLS 1.2 by default. It *can't* be done
        # for IoT Core anyway because the `System.Net.ServicePointManager` type doesn't exist in its version of dotnet.
        [System.Net.ServicePointManager]::SecurityProtocol =
            [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
    }

    if ($ExistingConfig -and (-not (Test-Path "$EdgeInstallDirectory\config.yaml"))) {
        Write-HostRed
        Write-HostRed "$EdgeInstallDirectory\config.yaml was not found."
        return
    }

    if ((-not $ExistingConfig) -and (Test-Path "$EdgeInstallDirectory\config.yaml")) {
        Write-HostRed
        Write-HostRed "$EdgeInstallDirectory\config.yaml already exists."
        Write-HostRed (
            'Delete it using `Uninstall-SecurityDaemon -Force -DeleteConfig` and then re-run `Install-SecurityDaemon`, ' +
            'or run `Install-SecurityDaemon -ExistingConfig` to use the existing config.yaml')
        return
    }

    # Download
    Get-SecurityDaemon
    Get-VcRuntime

    # config.yaml
    if ($ExistingConfig) {
        Write-HostGreen 'Using existing config.yaml'
    }
    else {
        Write-Host 'Generating config.yaml...'

        Set-ProvisioningMode
        Set-AgentImage
        Set-Hostname
        if ($ContainerOs -eq 'Linux') {
            Set-GatewayAddress
        }
        Set-MobyEngineParameters
    }

    # Register services
    Set-SystemPath
    Add-IotEdgeRegistryValues
    Install-Services
    if ($ContainerOs -eq 'Linux') {
        Add-FirewallExceptions
    }

    Write-HostGreen
    Write-HostGreen 'This device is now provisioned with the IoT Edge runtime.'
    Write-HostGreen 'Check the status of the IoT Edge service with `Get-Service iotedge`'
    Write-HostGreen 'List running modules with `iotedge list`'
    Write-HostGreen 'Display logs from the last five minutes in chronological order with'
    Write-HostGreen '    Get-WinEvent -ea SilentlyContinue -FilterHashtable @{ProviderName=''iotedged'';LogName=''application'';StartTime=[datetime]::Now.AddMinutes(-5)} |'
    Write-HostGreen '    Select TimeCreated, Message |'
    Write-HostGreen '    Sort-Object @{Expression=''TimeCreated'';Descending=$false} |'
    Write-HostGreen '    Format-Table -AutoSize -Wrap'
}

<#
.SYNOPSIS

Uninstalls the IoT Edge Security Daemon and its dependencies.


.DESCRIPTION

By default this commandlet does not delete the config.yaml and the Moby Engine data root (for -ContainerOs 'Windows' installs),
so that you can update the daemon using `Install-SecurityDaemon -ExistingConfig`. To delete these as well, specify the
-DeleteConfig and -DeleteMobyDataRoot flags.


.INPUTS

None


.OUTPUTS

None


.EXAMPLE

PS> Uninstall-SecurityDaemon


.EXAMPLE

PS> Uninstall-SecurityDaemon -DeleteConfig -DeleteMobyDataRoot


.EXAMPLE

PS> Uninstall-SecurityDaemon -Force
#>
function Uninstall-SecurityDaemon {
    [CmdletBinding()]
    param (
        # If set, the config.yaml will also be deleted.
        [Switch] $DeleteConfig,

        # If set, the Moby Engine data root will also be deleted. This directory is only created for -ContainerOs 'Windows' installs.
        [Switch] $DeleteMobyDataRoot,

        # Forces the uninstallation in case the previous install was only partially successful.
        [Switch] $Force
    )

    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version 5

    if (-not $Force -and -not ((Test-EdgeAlreadyInstalled) -or (Test-MobyAlreadyInstalled))) {
        Write-HostRed
        Write-HostRed 'IoT Edge is not installed. Use `-Force` to uninstall anyway.'
        return
    }

    Write-Host 'Uninstalling...'

    $ContainerOs = Get-ContainerOs

    Uninstall-Services
    $success = Remove-SecurityDaemonResources
    Reset-SystemPath

    Remove-MachineEnvironmentVariable 'IOTEDGE_HOST'
    Remove-Item Env:\IOTEDGE_HOST -ErrorAction SilentlyContinue

    Remove-FirewallExceptions

    if ($success) {
        Write-HostGreen 'Successfully uninstalled IoT Edge.'
    }
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

                $dockerCliExe = "$env:ProgramFiles\Docker\Docker\DockerCli.exe"

                if (-not (Test-Path -Path $dockerCliExe)) {
                    throw 'Unable to switch to Linux containers.'
                }

                Invoke-Native """$dockerCliExe"" -SwitchDaemon"

                if ((Get-ExternalDockerServerOs) -ne 'Linux') {
                    throw 'Unable to switch to Linux containers.'
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
    (Get-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion').'CurrentBuild'
}

function Test-EdgeAlreadyInstalled {
    (Get-Service $EdgeServiceName -ErrorAction SilentlyContinue) -or (Test-Path -Path "$EdgeInstallDirectory\iotedged.exe")
}

function Test-MobyAlreadyInstalled {
    (Get-Service $MobyServiceName -ErrorAction SilentlyContinue) -or (Test-Path -Path $MobyInstallDirectory)
}

function Test-MobyNeedsToBeMoved {
    if ($MobyStaticInstallDirectory -ne $MobyInstallDirectory) {
        return $(Test-Path -Path $MobyStaticInstallDirectory)
    }
    else {
        return $false
    }
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
    if ((Test-Path $MobyInstallDirectory) -or (Test-Path $MobyDataRootDirectory)) {
        return 'Windows'
    }
    else {
        return 'Linux'
    }
}

function Get-ExternalDockerServerOs {
    $dockerExe = Get-DockerExePath
    if ((Invoke-Native "$dockerExe version --format ""{{.Server.Os}}""" -Passthru) -match '\s*windows\s*$') {
        return 'Windows'
    }
    else {
        return 'Linux'
    }
}

function Get-SecurityDaemon {
    try {
        # If we create these archives ourselves, then delete them when we're done
        $deleteMobyEngineArchive = $false
        $deleteMobyCliArchive = $false
        $deleteEdgeArchive = $false

        if ($ContainerOs -eq 'Windows') {
            New-Item -Type Directory $MobyInstallDirectory | Out-Null
            Remove-BuiltinWritePermissions $MobyInstallDirectory
            $mobyEngineArchivePath =
                Download-File `
                    -Description 'Moby Engine' `
                    -Url 'https://aka.ms/iotedge-moby-engine-win-amd64-latest' `
                    -DownloadFilename 'iotedge-moby-engine.zip' `
                    -LocalCacheGlob '*moby-engine*.zip' `
                    -Delete ([ref] $deleteMobyEngineArchive)
            Expand-Archive $mobyEngineArchivePath $MobyInstallDirectory -Force

            New-Item -Type Directory $MobyDataRootDirectory -Force | Out-Null
            Remove-BuiltinWritePermissions $MobyDataRootDirectory

            if (-not ($SkipMobyCli)) {
                $mobyCliArchivePath =
                    Download-File `
                        -Description 'Moby CLI' `
                        -Url 'https://aka.ms/iotedge-moby-cli-win-amd64-latest' `
                        -DownloadFilename 'iotedge-moby-cli.zip' `
                        -LocalCacheGlob '*moby-cli*.zip' `
                        -Delete ([ref] $deleteMobyCliArchive)
                Expand-Archive $mobyCliArchivePath $MobyInstallDirectory -Force
            }
        }

        # Historically the `-ArchivePath` parameter pointed to the zip / directory of iotedged.
        # This is now better handled through `-OfflineInstallationPath`, but `-ArchivePath` is still allowed
        # for backward compatibility.
        if ($ArchivePath -ne '') {
            $edgeArchivePath = $ArchivePath
            $deleteEdgeArchive = $false
        }
        else {
            # The -LocalCacheGlob value here *intentionally* doesn't check for .zip extension,
            # so that an expanded directory of the same name will match
            $edgeArchivePath =
                Download-File `
                    -Description 'IoT Edge security daemon' `
                    -Url 'https://aka.ms/iotedged-windows-latest' `
                    -DownloadFilename 'iotedged-windows.zip' `
                    -LocalCacheGlob '*iotedged-windows*' `
                    -Delete ([ref] $deleteEdgeArchive)
        }

        if ((Get-Item $edgeArchivePath).PSIsContainer) {
            New-Item -Type Directory $EdgeInstallDirectory -Force | Out-Null
            if ($ExistingConfig) {
                Copy-Item "$edgeArchivePath\*" $EdgeInstallDirectory -Force -Recurse -Exclude 'config.yaml'
            }
            else {
                Copy-Item "$edgeArchivePath\*" $EdgeInstallDirectory -Force -Recurse
            }
        }
        else {
            New-Item -Type Directory $EdgeInstallDirectory -Force | Out-Null
            Expand-Archive $edgeArchivePath $EdgeInstallDirectory -Force
            if ($ExistingConfig) {
                Copy-Item "$EdgeInstallDirectory\iotedged-windows\*" $EdgeInstallDirectory -Force -Recurse -Exclude 'config.yaml'
            }
            else {
                Copy-Item "$EdgeInstallDirectory\iotedged-windows\*" $EdgeInstallDirectory -Force -Recurse
            }
        }

        Remove-BuiltinWritePermissions $EdgeInstallDirectory

        foreach ($name in 'mgmt', 'workload') {
            # We can't bind socket files directly in Windows, so create a folder
            # and bind to that. The folder needs to give Modify rights to a
            # well-known group that will exist in any container so that
            # non-privileged modules can access it.
            $path = "$EdgeInstallDirectory\$name"
            New-Item $Path -ItemType Directory -Force | Out-Null
            $sid = New-Object System.Security.Principal.SecurityIdentifier 'S-1-5-11' # NT AUTHORITY\Authenticated Users
            $rule = New-Object -TypeName System.Security.AccessControl.FileSystemAccessRule(`
                $sid, 'Modify', 'ObjectInherit', 'InheritOnly', 'Allow')
            $acl = Get-Acl -Path $path
            $acl.AddAccessRule($rule)
            Set-Acl -Path $path -AclObject $acl
        }

        New-Item -Type Directory $EdgeEventLogInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr | Out-Null
        if ($? -or ($cmdErr.FullyQualifiedErrorId -eq 'DirectoryExist,Microsoft.PowerShell.Commands.NewItemCommand')) {
            Remove-BuiltinWritePermissions $EdgeEventLogInstallDirectory
            Move-Item `
                "$EdgeInstallDirectory\iotedged_eventlog_messages.dll" `
                "$EdgeEventLogInstallDirectory\iotedged_eventlog_messages.dll" `
                -Force -ErrorAction SilentlyContinue -ErrorVariable cmdErr
            if ($?) {
                # Copied eventlog messages DLL successfully
            }
            elseif (
                ($cmdErr.Exception -is [System.IO.IOException]) -and
                ($cmdErr.Exception.HResult -eq 0x800700b7) # HRESULT_FROM_WIN32(ERROR_ALREADY_EXISTS)
            ) {
                # ERROR_ALREADY_EXISTS despite Move-Item -Force likely means the DLL is held open by something,
                # probably the Windows EventLog service or some other process.
                #
                # It's not really a problem to have an old DLL from a previous installation lying around, since the message IDs
                # and format strings haven't changed. Even if they have changed, it just means some logs in the event log will
                # not display correctly.
                #
                # Don't bother warning the user about it.
            }
            else {
                throw $cmdErr
            }
        }
        else {
            throw $cmdErr
        }
    }
    finally {
        Remove-Item "$EdgeInstallDirectory\iotedged-windows" -Recurse -Force -ErrorAction SilentlyContinue

        if ($deleteEdgeArchive) {
            Remove-Item $edgeArchivePath -Recurse -Force -ErrorAction SilentlyContinue
        }

        if ($deleteMobyEngineArchive) {
            Remove-Item $mobyEngineArchivePath -Recurse -Force -ErrorAction SilentlyContinue
        }

        if ($deleteMobyCliArchive) {
            Remove-Item $mobyCliArchivePath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Remove-SecurityDaemonResources {
    $success = $true

    foreach ($keyName in @($EdgeEventLogName, $MobyServiceName))
    {
        $logKey = Join-Path $EventLogApplicationRegPath $keyName
        Remove-Item $logKey -ErrorAction SilentlyContinue -ErrorVariable cmdErr
        Write-Verbose "$(if ($?) { "Deleted registry key '$logKey'" } else { $cmdErr })"
    }

    Write-Host "Deleting install directory '$EdgeInstallDirectory'..."
    if (-not $DeleteConfig) {
        Write-Host "Not deleting config.yaml since -DeleteConfig was not specified."
    }

    if ($DeleteConfig) {
        Remove-Item -Recurse $EdgeInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    }
    else {
        Remove-Item -Recurse $EdgeInstallDirectory -Exclude 'config.yaml' -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    }
    if ($?) {
        Write-Verbose "Deleted install directory '$EdgeInstallDirectory'"
    }
    elseif ($cmdErr.FullyQualifiedErrorId -ne 'PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand') {
        Write-Verbose "$cmdErr"
        Write-HostRed ("Could not delete install directory '$EdgeInstallDirectory'. Please reboot " +
            'your device and run `Uninstall-SecurityDaemon` again with `-Force`.')
        $success = $false
    }
    else {
        Write-Verbose "$cmdErr"
    }

    Remove-Item -Recurse $EdgeEventLogInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    if ($?) {
        Write-Verbose "Deleted install directory '$EdgeEventLogInstallDirectory'"
    }
    elseif ($cmdErr.FullyQualifiedErrorId -ne 'PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand') {
        Write-Verbose "$cmdErr"
        Write-Warning "Could not delete '$EdgeEventLogInstallDirectory'."
        Write-Warning 'If you''re reinstalling or updating IoT Edge, then this is safe to ignore.'
        Write-Warning ('Otherwise, please close Event Viewer, or any PowerShell windows where you ran Get-WinEvent, ' +
            'then run `Uninstall-SecurityDaemon` again with `-Force`.')
    }
    else {
        Write-Verbose "$cmdErr"
    }
    
    # Check whether we need to clean up after an errant installation into the OS partition on IoT Core
    if ($env:ProgramData -ne 'C:\ProgramData') {
        Write-Verbose "Multiple ProgramData directories found"
        $existingMobyDataRoots = $MobyDataRootDirectory, $MobyStaticDataRootDirectory 
        $existingMobyInstallations = $MobyInstallDirectory, $MobyStaticInstallDirectory 
    }
    else {
        $existingMobyDataRoots = $MobyDataRootDirectory
        $existingMobyInstallations = $MobyInstallDirectory
    }

    if ($DeleteMobyDataRoot) {
        foreach ($root in $existingMobyDataRoots | ?{ Test-Path $_ }) {
            try {
                Write-Host "Deleting Moby data root directory '$root'..."

                # Removing `$MobyDataRootDirectory` is tricky. Windows base images contain files owned by TrustedInstaller, etc
                # Deleting them is a three-step process:
                #
                # 1. Take ownership of all files
                Invoke-Native "takeown /r /skipsl /f ""$root"""

                # 2. Reset their ACLs so that they inherit from their container
                Invoke-Native "icacls ""$root"" /reset /t /l /q /c"

                # 3. Use cmd's `rd` rather than `Remove-Item` since the latter gets tripped up by reparse points, etc.
                #    Prepend the path with `\\?\` since the layer directories have long names, so the paths usually exceed 260 characters,
                #    and IoT Core's filesystem doesn't seem to automatically use (or even have) short names
                Invoke-Native "rd /s /q ""\\?\$root"""

                Write-Verbose "Deleted Moby data root directory '$root'"
            }
            catch {
                Write-Verbose "$_"
                Write-HostRed ("Could not delete Moby data root directory '$root'. Please reboot " +
                    'your device and run `Uninstall-SecurityDaemon` again with `-Force`.')
                $success = $false
            }
        }
    }
    else {
        Write-Host "Not deleting Moby data root directory since -DeleteMobyDataRoot was not specified."
    }

    foreach ($install in $existingMobyInstallations) {
        Remove-Item -Recurse $install -ErrorAction SilentlyContinue -ErrorVariable cmdErr
        if ($?) {
            Write-Verbose "Deleted install directory '$install'"
        }
        elseif ($cmdErr.FullyQualifiedErrorId -ne 'PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand') {
            Write-Verbose "$cmdErr"
            Write-HostRed ("Could not delete install directory '$install'. Please reboot " +
                'your device and run `Uninstall-SecurityDaemon` again with `-Force`.')
            $success = $false
        }
        else {
            Write-Verbose "$cmdErr"
        }
    }

    $success
}

function Get-MachineEnvironmentVariable([string] $Name) {
    # Equivalent to `[System.Environment]::GetEnvironmentVariable($Name, [System.EnvironmentVariableTarget]::Machine)`
    # but IoT Core doesn't have this overload

    (Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment').$Name
}

function Set-MachineEnvironmentVariable([string] $Name, [string] $Value) {
    # Equivalent to `[System.Environment]::SetEnvironmentVariable($Name, $Value, [System.EnvironmentVariableTarget]::Machine)`
    # but IoT Core doesn't have this overload

    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' -Name $Name -Value $Value
}

function Remove-MachineEnvironmentVariable([string] $Name) {
    # Equivalent to `[System.Environment]::SetEnvironmentVariable($Name, $null, [System.EnvironmentVariableTarget]::Machine)`
    # but IoT Core doesn't have this overload

    Remove-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' -Name $Name -ErrorAction SilentlyContinue
}

function Get-SystemPath {
    (Get-MachineEnvironmentVariable 'Path') -split ';' | Where-Object { $_.Length -gt 0 }
}

function Set-SystemPath {
    $systemPath = Get-SystemPath

    $needsModification = ($systemPath -notcontains $EdgeInstallDirectory) -or (($ContainerOs -eq 'Windows') -and ($systemPath -notcontains $MobyInstallDirectory));
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

    $needsModification = ($systemPath -contains $EdgeInstallDirectory) -or ($systemPath -contains $MobyInstallDirectory);
    if (-not $needsModification) {
        return
    }

    $systemPath = $systemPath | Where-Object { ($_ -ne $EdgeInstallDirectory) -and ($_ -ne $MobyInstallDirectory) }
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

    try {
        $vcRuntimeArchivePath =
            Download-File `
                -Description 'VC Runtime installer' `
                -Url 'https://download.microsoft.com/download/0/6/4/064F84EA-D1DB-4EAA-9A5C-CC2F0FF6A638/vc_redist.x64.exe' `
                -DownloadFilename 'vc_redist.x64.exe' `
                -LocalCacheGlob '*vc_redist*.exe' `
                -Delete ([ref] $deleteVcRuntimeArchive)

        Invoke-Native """$vcRuntimeArchivePath"" /quiet /norestart"
        Write-HostGreen 'Installed VC Runtime.'
    }
    catch {
        if ($LASTEXITCODE -eq 1638) {
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

function Install-Services {
    switch ($ContainerOs) {
        'Linux' {
            New-Service -Name $EdgeServiceName -BinaryPathName """$EdgeInstallDirectory\iotedged.exe"" -c ""$EdgeInstallDirectory\config.yaml""" | Out-Null
        }

        'Windows' {
            # dockerd needs two more slashes after the scheme
            $namedPipeUrl = $MobyNamedPipeUrl -replace 'npipe://\./pipe/', 'npipe:////./pipe/'
            New-Service -Name $MobyServiceName -BinaryPathName `
                """$MobyInstallDirectory\dockerd.exe"" -H $namedPipeUrl --exec-opt isolation=process --run-service --service-name ""$MobyServiceName"" --data-root ""$MobyDataRootDirectory""" | Out-Null
            New-Service -Name $EdgeServiceName -BinaryPathName """$EdgeInstallDirectory\iotedged.exe"" -c ""$EdgeInstallDirectory\config.yaml""" -DependsOn $MobyServiceName | Out-Null
        }
    }

    # Set service to restart after 1s if it fails
    Invoke-Native "sc.exe failure ""$EdgeServiceName"" actions= restart/1000 reset= 0"

    Start-Service $EdgeServiceName

    Write-HostGreen 'Initialized the IoT Edge service.'
}

function Uninstall-Services {
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

        if (Invoke-Native "sc.exe delete ""$EdgeServiceName""" -ErrorAction SilentlyContinue) {
            Write-Verbose 'Removed IoT Edge service subkey from the registry'
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

        if (Invoke-Native "sc.exe delete ""$MobyServiceName""" -ErrorAction SilentlyContinue) {
            Write-Verbose 'Removed IoT Edge Moby Engine service subkey from the registry'
        }
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

function Add-IotEdgeRegistryValues {
    $regKey = Join-Path $EventLogApplicationRegPath $EdgeEventLogName
    New-Item $regKey -Force | Out-Null
    New-ItemProperty $regKey -Name 'CustomSource' -Value 1 -PropertyType 'DWord' -Force | Out-Null
    New-ItemProperty $regKey -Name 'EventMessageFile' -Value "$EdgeEventLogInstallDirectory\iotedged_eventlog_messages.dll" -PropertyType 'String' -Force | Out-Null
    New-ItemProperty $regKey -Name 'TypesSupported' -Value 7 -PropertyType 'DWord' -Force | Out-Null

    if ($ContainerOs -eq 'Windows') {
        $regKey = Join-Path $EventLogApplicationRegPath $MobyServiceName
        New-Item $regKey -Force | Out-Null
        New-ItemProperty $regKey -Name 'CustomSource' -Value 1 -PropertyType 'DWord' -Force | Out-Null
        New-ItemProperty $regKey -Name 'EventMessageFile' -Value "$MobyInstallDirectory\dockerd.exe" -PropertyType 'String' -Force | Out-Null
        New-ItemProperty $regKey -Name 'TypesSupported' -Value 7 -PropertyType 'DWord' -Force | Out-Null
    }

    Write-HostGreen 'Added IoT Edge registry values.'
}

function Set-ProvisioningMode {
    $configurationYaml = Get-Content "$EdgeInstallDirectory\config.yaml" -Raw
    if (($Manual) -or ($DeviceConnectionString)) {
        $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*device_connection_string:\s*".*"'
        $replacementContent = @(
            'provisioning:',
            '  source: ''manual''',
            "  device_connection_string: '$DeviceConnectionString'")
        ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")) | Set-Content "$EdgeInstallDirectory\config.yaml" -Force
        Write-HostGreen 'Configured device for manual provisioning.'
    }
    else {
        $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*global_endpoint:\s*".*"\s*#?\s*scope_id:\s*".*"\s*#?\s*registration_id:\s".*"'
        $replacementContent = @(
            'provisioning:',
            '  source: ''dps''',
            '  global_endpoint: ''https://global.azure-devices-provisioning.net''',
            "  scope_id: '$ScopeId'",
            "  registration_id: '$RegistrationId'")
        $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")

        $selectionRegex = '(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*".*"\s*#?\s*device_connection_string:\s*".*"'
        $replacementContent = ''
        ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")) | Set-Content "$EdgeInstallDirectory\config.yaml" -Force

        New-Item "HKLM:\SYSTEM\CurrentControlSet\Services\$EdgeServiceName" -Force | Out-Null
        New-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$EdgeServiceName" `
            -Name 'Environment' -Value 'IOTEDGE_USE_TPM_DEVICE=ON' -PropertyType 'MultiString' -Force | Out-Null

        Write-HostGreen 'Configured device for DPS provisioning.'
    }
}

function Set-AgentImage {
    if ($AgentImage) {
        $yamlPath = "$EdgeInstallDirectory\config.yaml"
        $configurationYaml = Get-Content $yamlPath -Raw
        $selectionRegex = 'image:\s*".*"'
        $replacementContent = "image: '$AgentImage'"
        if ($Username -and $Password) {
            $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")
            $selectionRegex = 'auth:\s*\{\s*\}'
            $agentRegistry = Get-AgentRegistry
            $cred = New-Object System.Management.Automation.PSCredential ($Username, $Password)
            $replacementContent = @(
                'auth:',
                "      serveraddress: '$agentRegistry'",
                "      username: '$Username'",
                "      password: '$($cred.GetNetworkCredential().Password)'")
        }
        ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")) | Set-Content $yamlPath -Force
        Write-HostGreen "Configured device with agent image '$AgentImage'."
    }
}

function Set-Hostname {
    $configurationYaml = Get-Content "$EdgeInstallDirectory\config.yaml" -Raw
    $hostname = [System.Net.Dns]::GetHostName()
    $selectionRegex = 'hostname:\s*".*"'
    $replacementContent = "hostname: '$hostname'"
    ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")) | Set-Content "$EdgeInstallDirectory\config.yaml" -Force
    Write-HostGreen "Configured device with hostname '$hostname'."
}

function Set-GatewayAddress {
    $configurationYaml = Get-Content "$EdgeInstallDirectory\config.yaml" -Raw
    $gatewayAddress = (Get-NetIpAddress |
            Where-Object {$_.InterfaceAlias -like '*vEthernet (DockerNAT)*' -and $_.AddressFamily -eq 'IPv4'}).IPAddress

    $selectionRegex = 'connect:\s*management_uri:\s*".*"\s*workload_uri:\s*".*"'
    $replacementContent = @(
        'connect:',
        "  management_uri: 'http://${gatewayAddress}:15580'",
        "  workload_uri: 'http://${gatewayAddress}:15581'")
    $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")

    $selectionRegex = 'listen:\s*management_uri:\s*".*"\s*workload_uri:\s*".*"'
    $replacementContent = @(
        'listen:',
        "  management_uri: 'http://${gatewayAddress}:15580'",
        "  workload_uri: 'http://${gatewayAddress}:15581'")
    $configurationYaml = $configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")

    Set-MachineEnvironmentVariable 'IOTEDGE_HOST' "http://${gatewayAddress}:15580"
    $env:IOTEDGE_HOST = "http://${gatewayAddress}:15580"

    $configurationYaml | Set-Content "$EdgeInstallDirectory\config.yaml" -Force
    Write-HostGreen "Configured device with gateway address '$gatewayAddress'."
}

function Set-MobyEngineParameters {
    $configurationYaml = Get-Content "$EdgeInstallDirectory\config.yaml" -Raw
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
    ($configurationYaml -replace $selectionRegex, ($replacementContent -join "`n")) | Set-Content "$EdgeInstallDirectory\config.yaml" -Force
    Write-HostGreen "Configured device with Moby Engine URL '$mobyUrl' and network '$mobyNetwork'."
}

function Get-AgentRegistry {
    $parts = $AgentImage -split '/'
    if (($parts.Length -gt 1) -and ($parts[0] -match '\.')) {
        return $parts[0]
    }
    return 'index.docker.io'
}

function Remove-IotEdgeContainers {
    $dockerExe = Get-DockerExePath

    if (-not (Test-IsDockerRunning 6> $null)) {
        return
    }

    $allContainersString = Invoke-Native "$dockerExe ps --all --format ""{{.ID}}""" -Passthru
    [string[]] $allContainers = $allContainersString -split {$_ -eq "`r" -or $_ -eq "`n"} | where {$_.Length -gt 0}

    foreach ($containerId in $allContainers) {
        $inspectString = Invoke-Native "$dockerExe inspect ""$containerId""" -Passthru
        $inspectResult = ($inspectString | ConvertFrom-Json)[0]
        $label = $inspectResult.Config.Labels | Get-Member -MemberType NoteProperty -Name 'net.azure-devices.edge.owner' | %{ $inspectResult.Config.Labels | Select-Object -ExpandProperty $_.Name } 

        if ($label -eq 'Microsoft.Azure.Devices.Edge.Agent') {
            if (($inspectResult.Name -eq '/edgeAgent') -or ($inspectResult.Name -eq '/edgeHub')) {
                Invoke-Native "$dockerExe rm --force ""$containerId"""
                Write-Verbose "Stopped and deleted container $($inspectResult.Name)"

                # `.Config.Image` contains the user-provided name, but this can be a tag like `foo:latest`
                # that doesn't necessarily point to the image that has that tag right now.
                #
                # So delete the image using its unique identifier, as given by `.Image`, like `sha256:1234abcd...`
                Invoke-Native "$dockerExe image rm --force ""$($inspectResult.Image)"""
                Write-Verbose "Deleted image of container $($inspectResult.Name) ($($inspectResult.Config.Image))"
            }
            else {
                Invoke-Native "$dockerExe stop ""$containerId"""
                Write-Verbose "Stopped container $($inspectResult.Name)"
            }
        }
    }
}

function Get-DockerExePath {
    switch ($ContainerOs) {
        'Linux' {
            return '"docker"'
        }

        'Windows' {
            # docker needs two more slashes after the scheme
            $namedPipeUrl = $MobyNamedPipeUrl -replace 'npipe://\./pipe/', 'npipe:////./pipe/'
            return """docker"" -H ""$namedPipeUrl"""
        }
    }
}

function Invoke-Native {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [String] $Command,

        [Switch] $Passthru
    )

    process {
        Write-Verbose "Executing native Windows command '$Command'..."
        $out = cmd /c "($Command) 2>&1" 2>&1 | Out-String
        Write-Verbose $out
        Write-Verbose "Exit code: $LASTEXITCODE"

        if ($LASTEXITCODE) {
            throw $out
        }
        elseif ($Passthru) {
            $out
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

        Invoke-WebRequest `
            -Uri $Url `
            -OutFile "$env:TEMP\$DownloadFileName" `
            -UseBasicParsing `
            @InvokeWebRequestParameters

        $Delete.Value = $true
        $result = "$env:TEMP\$DownloadFileName"
    }

    Write-HostGreen "Using $Description from $result"
    return $result
}

Export-ModuleMember -Function Install-SecurityDaemon, Uninstall-SecurityDaemon
}
