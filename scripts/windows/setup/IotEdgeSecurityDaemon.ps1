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

Set-Variable DockerServiceName -Value 'docker' -Option Constant

Set-Variable EdgeInstallDirectory -Value 'C:\ProgramData\iotedge' -Option Constant
Set-Variable EdgeEventLogName -Value 'iotedged' -Option Constant
Set-Variable EdgeEventLogInstallDirectory -Value 'C:\ProgramData\iotedge-eventlog' -Option Constant
Set-Variable EdgeServiceName -Value 'iotedge' -Option Constant

Set-Variable MobyInstallDirectory -Value 'C:\ProgramData\iotedge-moby' -Option Constant
Set-Variable MobyNamedPipeUrl -Value 'npipe://./pipe/iotedge_moby_engine' -Option Constant
Set-Variable MobyServiceName -Value 'iotedge-moby' -Option Constant

enum ContainerOs {
    Linux
    Windows
}

function Install-SecurityDaemon {
    [CmdletBinding()]
    param (
        [Parameter(ParameterSetName = 'Manual')]
        [Switch] $Manual,

        [Parameter(ParameterSetName = 'DPS')]
        [Switch] $Dps,

        [Parameter(Mandatory = $true, ParameterSetName = 'Manual')]
        [String] $DeviceConnectionString,

        [Parameter(Mandatory = $true, ParameterSetName = 'DPS')]
        [String] $ScopeId,

        [Parameter(Mandatory = $true, ParameterSetName = 'DPS')]
        [String] $RegistrationId,

        [ContainerOs] $ContainerOs = 'Linux',

        # Proxy URI
        [Uri] $Proxy,

        # Local path to iotedged zip file
        [String] $ArchivePath,

        # IoT Edge Agent image to pull
        [String] $AgentImage,

        # Username to pull IoT Edge Agent image
        [String] $Username,

        # Password to pull IoT Edge Agent image
        [SecureString] $Password
    )

    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version 5

    if (Test-EdgeAlreadyInstalled) {
        Write-HostRed
        Write-HostRed 'IoT Edge is already installed. To reinstall, run `Uninstall-SecurityDaemon` first.'
        return
    }

    if (Test-MobyAlreadyInstalled) {
        Write-HostRed
        Write-HostRed 'IoT Edge Moby Runtime is already installed. To reinstall, run `Uninstall-SecurityDaemon` first.'
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

    # Download
    Get-SecurityDaemon
    Get-VcRuntime

    # Update config.yaml
    Set-ProvisioningMode
    Set-AgentImage
    Set-Hostname
    Set-MobyRuntimeParameters

    # Register services
    Set-SystemPath
    Add-IotEdgeRegistryKey
    Install-Services

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

function Uninstall-SecurityDaemon {
    [CmdletBinding()]
    param (
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
    Stop-IotEdgeContainers
    $success = Remove-SecurityDaemonResources
    Reset-SystemPath
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

            $dockerCliExe = "$env:ProgramFiles\Docker\Docker\DockerCli.exe"

            if ((Get-ExternalDockerServerOs) -ne 'Linux') {
                Write-Host 'Switching Docker to use Linux containers...'

                if (-not (Test-Path -Path $dockerCliExe)) {
                    throw 'Unable to switch to Linux containers.'
                }

                Invoke-Native """$dockerCliExe"" -SwitchDaemon"

                if ((Get-ExternalDockerServerOs) -ne 'Linux') {
                    throw 'Unable to switch to Linux containers.'
                }

                Write-HostGreen 'Switched Docker to use Linux containers.'
            }

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

function Get-WindowsBuild {
    (Get-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion').'CurrentBuild'
}

function Test-EdgeAlreadyInstalled {
    (Get-Service $EdgeServiceName -ErrorAction SilentlyContinue) -or (Test-Path -Path $EdgeInstallDirectory)
}

function Test-MobyAlreadyInstalled {
    (Get-Service $MobyServiceName -ErrorAction SilentlyContinue) -or (Test-Path -Path $MobyInstallDirectory)
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
        Write-HostRed $Message
    }
    return $valid
}

function Get-ContainerOs {
    if (Test-Path $MobyInstallDirectory) {
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
        # If we create the iotedge archive ourselves, then delete it when we're done
        $deleteEdgeArchive = $false

        $mobyArchivePath = "$env:TEMP\iotedge-moby.zip"

        if (-not "$ArchivePath") {
            $ArchivePath = "$env:TEMP\iotedged-windows.zip"
            $deleteEdgeArchive = $true
            Write-Host 'Downloading the latest version of IoT Edge security daemon...'
            Invoke-WebRequest `
                -Uri 'https://aka.ms/iotedged-windows-latest' `
                -OutFile $ArchivePath `
                -UseBasicParsing `
                -Proxy $Proxy
            Write-HostGreen 'Downloaded security daemon.'
        }
        if ((Get-Item $ArchivePath).PSIsContainer) {
            New-Item -Type Directory $EdgeInstallDirectory | Out-Null
            Copy-Item "$ArchivePath\*" $EdgeInstallDirectory -Force
        }
        else {
            New-Item -Type Directory $EdgeInstallDirectory | Out-Null
            Expand-Archive $ArchivePath $EdgeInstallDirectory -Force
            Copy-Item "$EdgeInstallDirectory\iotedged-windows\*" $EdgeInstallDirectory -Force -Recurse
        }

        foreach ($name in 'mgmt', 'workload') {
            # We can't bind socket files directly in Windows, so create a folder
            # and bind to that. The folder needs to give Modify rights to a
            # well-known group that will exist in any container so that
            # non-privileged modules can access it.
            $path = "$EdgeInstallDirectory\$name"
            New-Item $Path -ItemType Directory -Force | Out-Null
            $rule = New-Object -TypeName System.Security.AccessControl.FileSystemAccessRule(`
                'NT AUTHORITY\Authenticated Users', 'Modify', 'ObjectInherit', 'InheritOnly', 'Allow')
            $acl = Get-Acl -Path $path
            $acl.AddAccessRule($rule)
            Set-Acl -Path $path -AclObject $acl
        }

        New-Item -Type Directory $EdgeEventLogInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr | Out-Null
        if ($? -or ($cmdErr.FullyQualifiedErrorId -eq 'DirectoryExist,Microsoft.PowerShell.Commands.NewItemCommand')) {
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

        if ($ContainerOs -eq 'Windows') {
            # Get moby runtime

            Write-Host 'Downloading the latest version of Moby runtime...'
            Invoke-WebRequest `
                -Uri 'https://aka.ms/iotedge-moby-win-amd64-latest' `
                -OutFile $mobyArchivePath `
                -UseBasicParsing `
                -Proxy $Proxy
            Write-HostGreen 'Downloaded moby runtime.'

            New-Item -Type Directory $MobyInstallDirectory | Out-Null
            Expand-Archive $mobyArchivePath $MobyInstallDirectory -Force
            Copy-Item "$MobyInstallDirectory\docker\*" $MobyInstallDirectory -Force -Recurse
        }
    }
    finally {
        Remove-Item "$EdgeInstallDirectory\iotedged-windows" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$MobyInstallDirectory\docker" -Recurse -Force -ErrorAction SilentlyContinue

        if ($deleteEdgeArchive) {
            Remove-Item $ArchivePath -Recurse -Force -ErrorAction SilentlyContinue
        }

        Remove-Item $mobyArchivePath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Remove-SecurityDaemonResources {
    $success = $true

    $logKey = "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\$EdgeEventLogName"
    Remove-Item $logKey -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    Write-Verbose "$(if ($?) { "Deleted registry key '$logKey'" } else { $cmdErr })"

    Remove-Item -Recurse $EdgeInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr
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

    Remove-Item -Recurse $MobyInstallDirectory -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    if ($?) {
        Write-Verbose "Deleted install directory '$MobyInstallDirectory'"
    }
    elseif ($cmdErr.FullyQualifiedErrorId -ne 'PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand') {
        Write-Verbose "$cmdErr"
        Write-HostRed ("Could not delete install directory '$MobyInstallDirectory'. Please reboot " +
            'your device and run `Uninstall-SecurityDaemon` again with `-Force`.')
        $success = $false
    }
    else {
        Write-Verbose "$cmdErr"
    }

    $success
}

function Get-SystemPath {
    [System.Environment]::GetEnvironmentVariable('PATH', [System.EnvironmentVariableTarget]::Machine) -split ';' | Where-Object { $_.Length -gt 0 }
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

    [System.Environment]::SetEnvironmentVariable('PATH', ($systemPath -join ';'), [System.EnvironmentVariableTarget]::Machine)
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
    [System.Environment]::SetEnvironmentVariable('PATH', ($systemPath -join ';'), [System.EnvironmentVariableTarget]::Machine)

    Write-Verbose 'Removed IoT Edge directories from system PATH'
}

function Get-VcRuntime {
    if (Test-IotCore) {
        Write-HostGreen 'Skipped vcruntime download on IoT Core.'
        return
    }

    try {
        Write-Host 'Downloading vcruntime...'
        Invoke-WebRequest `
            -Uri 'https://download.microsoft.com/download/0/6/4/064F84EA-D1DB-4EAA-9A5C-CC2F0FF6A638/vc_redist.x64.exe' `
            -OutFile "$env:TEMP\vc_redist.exe" `
            -UseBasicParsing `
            -Proxy $Proxy
        Invoke-Native """$env:TEMP\vc_redist.exe"" /quiet /norestart"
        Write-HostGreen 'Downloaded vcruntime.'
    }
    catch {
        if ($LASTEXITCODE -eq 1638) {
            Write-HostGreen 'Skipping vcruntime installation because a newer version is already installed.'
        }
        else {
            throw $_
        }
    }
    finally {
        Remove-Item "$env:TEMP\vc_redist.exe" -Force -Recurse -ErrorAction SilentlyContinue
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
                """$MobyInstallDirectory\dockerd.exe"" -H $namedPipeUrl --exec-opt isolation=process --run-service --service-name ""$MobyServiceName""" | Out-Null
            New-Service -Name $EdgeServiceName -BinaryPathName """$EdgeInstallDirectory\iotedged.exe"" -c ""$EdgeInstallDirectory\config.yaml""" -DependsOn $MobyServiceName | Out-Null
        }
    }

    Start-Service $EdgeServiceName

    Write-HostGreen 'Initialized the IoT Edge service.'
}

function Uninstall-Services {
    if (Get-Service $EdgeServiceName -ErrorAction SilentlyContinue) {
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

    if (Get-Service $MobyServiceName -ErrorAction SilentlyContinue) {
        Stop-Service -NoWait -ErrorAction SilentlyContinue -ErrorVariable cmdErr $MobyServiceName
        if ($?) {
            Start-Sleep -Seconds 7
            Write-Verbose 'Stopped the IoT Edge Moby Runtime service'
        }
        else {
            Write-Verbose "$cmdErr"
        }

        if (Invoke-Native "sc.exe delete ""$MobyServiceName""" -ErrorAction SilentlyContinue) {
            Write-Verbose 'Removed IoT Edge Moby Runtime service subkey from the registry'
        }
    }
}

# The daemon listens using UDS by default, so these firewall rules are no longer created.
# However Uninstall-SecurityDaemon still calls this to remove rules created by old installs.
function Remove-FirewallExceptions {
    Remove-NetFirewallRule -DisplayName 'iotedged allow inbound 15580,15581' -ErrorAction SilentlyContinue -ErrorVariable cmdErr
    Write-Verbose "$(if ($?) { 'Removed firewall exceptions' } else { $cmdErr })"
}

function Add-IotEdgeRegistryKey {
    New-Item 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application' -Name $EdgeEventLogName -Force | Out-Null
    New-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\$EdgeEventLogName" `
        -Name 'CustomSource' -Value 1 -PropertyType 'DWord' -Force | Out-Null
    New-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\$EdgeEventLogName" `
        -Name 'EventMessageFile' -Value "$EdgeEventLogInstallDirectory\iotedged_eventlog_messages.dll" -PropertyType 'String' -Force | Out-Null
    New-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\$EdgeEventLogName" `
        -Name 'TypesSupported' -Value 7 -PropertyType 'DWord' -Force | Out-Null

    Write-HostGreen 'Added IoT Edge registry key.'
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

function Set-MobyRuntimeParameters {
    $configurationYaml = Get-Content "$EdgeInstallDirectory\config.yaml" -Raw
    $selectionRegex = 'moby_runtime:\s*uri:\s*".*"\s*#?\s*network:\s*".*"'
    $replacementContentWindows = @(
        'moby_runtime:',
        "  uri: '$MobyNamedPipeUrl'",
        '  network: ''nat''')
    $replacementContentLinux = @(
        'moby_runtime:',
        '  uri: ''npipe://./pipe/docker_engine''',
        '  network: ''azure-iot-edge''')
    switch ($ContainerOs) {
        'Linux' {
            ($configurationYaml -replace $selectionRegex, ($replacementContentLinux -join "`n")) | Set-Content "$EdgeInstallDirectory\config.yaml" -Force
            Write-HostGreen 'Set the Moby runtime network to ''azure-iot-edge''.'
        }

        'Windows' {
            ($configurationYaml -replace $selectionRegex, ($replacementContentWindows -join "`n")) | Set-Content "$EdgeInstallDirectory\config.yaml" -Force
            Write-HostGreen 'Set the Moby runtime network to ''nat''.'
        }
    }
}

function Get-AgentRegistry {
    $parts = $AgentImage -split '/'
    if (($parts.Length -gt 1) -and ($parts[0] -match '\.')) {
        return $parts[0]
    }
    return 'index.docker.io'
}

function Stop-IotEdgeContainers {
    $dockerExe = Get-DockerExePath

    if (-not (Test-IsDockerRunning 6> $null)) {
        return
    }

    $runningContainersString = Invoke-Native "$dockerExe ps --format ""{{.ID}}""" -Passthru
    [string[]] $runningContainers = $runningContainersString -split {$_ -eq "`r" -or $_ -eq "`n"} | where {$_.Length -gt 0}

    foreach ($containerId in $runningContainers) {
        $inspectString = Invoke-Native "$dockerExe inspect ""$containerId""" -Passthru | ConvertFrom-Json
        $inspectResult = ($inspectString | ConvertFrom-Json)[0]

        if ($inspectResult.Config.Labels.'net.azure-devices.edge.owner' -eq 'Microsoft.Azure.Devices.Edge.Agent') {
            Invoke-Native "$dockerExe stop ""$containerId"""
            Write-Verbose "Stopped container $($inspectResult.Name)"
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

Export-ModuleMember -Function Install-SecurityDaemon, Uninstall-SecurityDaemon
}
