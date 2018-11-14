New-Module -Name IotEdgeSecurityDaemon -ScriptBlock {

[Console]::OutputEncoding = New-Object -typename System.Text.ASCIIEncoding

<#
 # Installs the IoT Edge Security Daemon on Windows.
 #>

#requires -Version 5
#requires -RunAsAdministrator

function Install-SecurityDaemon {
    [CmdletBinding()]
    param (
        [Parameter(ParameterSetName = "Manual")]
        [Switch] $Manual,

        [Parameter(ParameterSetName = "DPS")]
        [Switch] $Dps,

        [Parameter(Mandatory = $true, ParameterSetName = "Manual")]
        [String] $DeviceConnectionString,

        [Parameter(Mandatory = $true, ParameterSetName = "DPS")]
        [String] $ScopeId,

        [Parameter(Mandatory = $true, ParameterSetName = "DPS")]
        [String] $RegistrationId,

        [ValidateSet("Linux", "Windows")]
        [String] $ContainerOs = "Linux",

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

    $ErrorActionPreference = "Stop"
    Set-StrictMode -Version 5

    Set-Variable Windows1607 -Value 14393 -Option Constant
    Set-Variable Windows1803 -Value 17134 -Option Constant
    Set-Variable Windows1809 -Value 17763 -Option Constant

    if (Test-EdgeAlreadyInstalled) {
        Write-Host ("`nIoT Edge is already installed. To reinstall, run 'Uninstall-SecurityDaemon' first.") `
            -ForegroundColor "Red"
        return
    }

    if (-not (Test-AgentRegistryArgs)) {
        return
    }

    if (-not (Test-IsDockerRunning) -or -not (Test-IsKernelValid)) {
        Write-Host ("`nThe prerequisites for installation of the IoT Edge Security daemon are not met. " +
            "Please fix all known issues before rerunning this script.") `
            -ForegroundColor "Red"
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

    $usesSeparateDllForEventLogMessages = Get-SecurityDaemon
    Set-SystemPath
    Get-VcRuntime
    Add-FirewallExceptions
    Add-IotEdgeRegistryKey -UsesSeparateDllForEventLogMessages:$usesSeparateDllForEventLogMessages

    Set-ProvisioningMode
    Set-AgentImage
    Set-Hostname
    Set-MobyNetwork
    if (-not (Test-UdsSupport)) {
        Set-GatewayAddress
    }
    Install-IotEdgeService

    Write-Host ("`nThis device is now provisioned with the IoT Edge runtime.`n" +
        "Check the status of the IoT Edge service with `"Get-Service iotedge`"`n" +
        "List running modules with `"iotedge list`"`n" +
        "Display logs from the last five minutes in chronological order with`n" +
        "    Get-WinEvent -ea SilentlyContinue -FilterHashtable @{ProviderName=`"iotedged`";LogName=`"application`";StartTime=[datetime]::Now.AddMinutes(-5)} |`n" +
        "    Select TimeCreated, Message |`n" +
        "    Sort-Object @{Expression=`"TimeCreated`";Descending=`$false} |`n" +
        "    Format-Table -AutoSize -Wrap") `
        -ForegroundColor "Green"
}

function Uninstall-SecurityDaemon {
    [CmdletBinding()]
    param (
        [Switch] $Force
    )

    $ErrorActionPreference = "Stop"
    Set-StrictMode -Version 5

    if (-not $Force -and -not (Test-EdgeAlreadyInstalled)) {
        Write-Host ("`nIoT Edge is not installed. Use '-Force' to uninstall anyway.") `
            -ForegroundColor "Red"
        return
    }

    Write-Host "Uninstalling..."

    $ContainerOs = Get-ContainerOs

    Uninstall-IotEdgeService
    Stop-IotEdgeContainers
    $success = Remove-SecurityDaemonResources
    Reset-SystemPath
    Remove-FirewallExceptions

    if ($success) {
        Write-Host "Successfully uninstalled IoT Edge." -ForegroundColor "Green"
    }
}

function Test-IsDockerRunning {
    $DockerService = Get-Service "*docker*"
    if ($DockerService -and $DockerService.Status -eq "Running") {
        Write-Host "Docker is running." -ForegroundColor "Green"
        $DockerCliExe = "$env:ProgramFiles\Docker\Docker\DockerCli.exe"
        if ($ContainerOs -eq "Windows" -and (Get-ContainerOs) -ne "Windows") {
            if (-not (Test-Path -Path $DockerCliExe)) {
                throw "Unable to switch to Windows containers."
            }
            Write-Host "Switching Docker to use Windows containers" -ForegroundColor "Green"
            Invoke-Native "`"$DockerCliExe`" -SwitchDaemon"
            if ((Get-ContainerOs) -ne "Windows") {
                throw "Unable to switch to Windows containers."
            }
        } elseif ($ContainerOs -eq "Linux" -and (Get-ContainerOs) -ne "Linux") {
            if (-not (Test-Path -Path $DockerCliExe)) {
                throw "Unable to switch to Linux containers."
            }
            Write-Host "Switching Docker to use Linux containers" -ForegroundColor "Green"
            Invoke-Native "`"$DockerCliExe`" -SwitchDaemon"
            if ((Get-ContainerOs) -ne "Linux") {
                throw "Unable to switch to Linux containers."
            }
        }
    } else {
        Write-Host "Docker is not running." -ForegroundColor "Red"
        if (Test-IotCore) {
            Write-Host ("Please visit https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-core " +
                "for assistance with installing Docker on IoT Core.") `
                -ForegroundColor "Red"
        }
        else {
            Write-Host ("You can use Docker for Windows for development and testing. " +
                "Please visit https://www.docker.com/docker-windows for additional information.") `
                -ForegroundColor "Red"
        }
        return $false
    }
    return $true
}

function Get-WindowsBuild {
    return (Get-Item "HKLM:\Software\Microsoft\Windows NT\CurrentVersion").GetValue("CurrentBuild")
}

function Test-UdsSupport {
    # TODO: Enable when we have RS5-based modules in process-isolated containers
    # $MinBuildForUnixDomainSockets = $Windows1809
    # $CurrentBuild = Get-WindowsBuild
    # return ($ContainerOs -eq "Windows" -and $CurrentBuild -ge $MinBuildForUnixDomainSockets)
    return $false
}

function Test-IsKernelValid {
    $MinBuildForLinuxContainers = $Windows1607
    $SupportedBuildsForWindowsContainers = @($Windows1803, $Windows1809)
    $CurrentBuild = Get-WindowsBuild

    if (($ContainerOs -eq "Linux" -and $CurrentBuild -ge $MinBuildForLinuxContainers) -or `
        ($ContainerOs -eq "Windows" -and $SupportedBuildsForWindowsContainers -contains $CurrentBuild)) {
        Write-Host "The container host is on supported build version $CurrentBuild." -ForegroundColor "Green"
        return $true
    } else {
        Write-Host ("The container host is on unsupported build version $CurrentBuild. `n" +
            "Please use a container host running build $MinBuildForLinuxContainers newer when using Linux containers" +
            "or with one of the following supported build versions when using Windows containers:`n" +
            ($SupportedBuildsForWindowsContainers -join "`n")) `
            -ForegroundColor "Red"
        return $false
    }
}

function Test-EdgeAlreadyInstalled {
    $ServiceName = "iotedge"
    $IotEdgePath = "C:\ProgramData\iotedge"

    if ((Get-Service $ServiceName -ErrorAction SilentlyContinue) -or (Test-Path -Path $IotEdgePath)) {
        return $true
    }
    return $false
}

function Test-AgentRegistryArgs {
    $NoImageNoCreds = (-not ($AgentImage -or $Username -or $Password))
    $ImageNoCreds = ($AgentImage -and -not ($Username -or $Password))
    $ImageFullCreds = ($AgentImage -and $Username -and $Password)

    $Valid = ($NoImageNoCreds -or $ImageNoCreds -or $ImageFullCreds)
    if (-not $Valid) {
        $Message =
            if (-not $AgentImage) {
                "Parameter 'AgentImage' is required when parameters 'Username' and 'Password' are specified."
            }
            else {
                "Parameters 'Username' and 'Password' must be used together. Please specify both (or neither for anonymous access)."
            }
        Write-Host $Message -ForegroundColor Red
    }
    return $Valid
}

function Get-ContainerOs {
    if ((Invoke-Native "docker version --format {{.Server.Os}}" -Passthru) -match "\s*windows\s*$") {
        return "Windows"
    }
    else {
        return "Linux"
    }
}

function Get-SecurityDaemon {
    try {
        $DeleteArchive = $false
        if (-not "$ArchivePath") {
            $ArchivePath = "$env:TEMP\iotedged-windows.zip"
            $DeleteArchive = $true
            Write-Host "Downloading the latest version of IoT Edge security daemon." -ForegroundColor "Green"
            Invoke-WebRequest `
                -Uri "https://aka.ms/iotedged-windows-latest" `
                -OutFile "$ArchivePath" `
                -UseBasicParsing `
                -Proxy $Proxy
            Write-Host "Downloaded security daemon." -ForegroundColor "Green"
        }
        if ((Get-Item "$ArchivePath").PSIsContainer) {
            New-Item -Type Directory 'C:\ProgramData\iotedge' | Out-Null
            Copy-Item "$ArchivePath\*" "C:\ProgramData\iotedge" -Force
        }
        else {
            New-Item -Type Directory 'C:\ProgramData\iotedge' | Out-Null
            Expand-Archive "$ArchivePath" "C:\ProgramData\iotedge" -Force
            Copy-Item "C:\ProgramData\iotedge\iotedged-windows\*" "C:\ProgramData\iotedge" -Force -Recurse
        }

        if (Test-UdsSupport) {
            foreach ($Name in "mgmt", "workload")
            {
                # We can't bind socket files directly in Windows, so create a folder
                # and bind to that. The folder needs to give Modify rights to a
                # well-known group that will exist in any container so that
                # non-privileged modules can access it.
                $Path = "C:\ProgramData\iotedge\$Name"
                New-Item "$Path" -ItemType "Directory" -Force | Out-Null
                $Rule = New-Object -TypeName System.Security.AccessControl.FileSystemAccessRule(`
                    "NT AUTHORITY\Authenticated Users", 'Modify', 'ObjectInherit', 'InheritOnly', 'Allow')
                $Acl = [System.IO.Directory]::GetAccessControl($Path)
                $Acl.AddAccessRule($Rule)
                [System.IO.Directory]::SetAccessControl($Path, $Acl)            
            }
        }

        if (Test-Path 'C:\ProgramData\iotedge\iotedged_eventlog_messages.dll') {
            # This release uses iotedged_eventlog_messages.dll as the eventlog message file

            New-Item -Type Directory 'C:\ProgramData\iotedge-eventlog' -ErrorAction SilentlyContinue -ErrorVariable CmdErr | Out-Null
            if ($? -or ($CmdErr.FullyQualifiedErrorId -eq 'DirectoryExist,Microsoft.PowerShell.Commands.NewItemCommand')) {
                Move-Item `
                    'C:\ProgramData\iotedge\iotedged_eventlog_messages.dll' `
                    'C:\ProgramData\iotedge-eventlog\iotedged_eventlog_messages.dll' `
                    -Force -ErrorAction SilentlyContinue -ErrorVariable CmdErr
                if ($?) {
                    # Copied eventlog messages DLL successfully
                }
                elseif (
                    ($CmdErr.Exception -is [System.IO.IOException]) -and
                    ($CmdErr.Exception.HResult -eq 0x800700b7) # HRESULT_FROM_WIN32(ERROR_ALREADY_EXISTS)
                ) {
                    # ERROR_ALREADY_EXISTS despite Move-Item -Force likely means the DLL is held open by something,
                    # probably the Windows EventLog service or some other process.
                    #
                    # It's not really a problem to have an old DLL from a previous installation lying around, since the message IDs
                    # and format strings haven't changed. Even if they have changed, it just means some logs in the event log will
                    # not display correcty.
                    #
                    # Don't bother warning the user about it.
                }
                else {
                    throw $CmdErr
                }
            }
            else {
                throw $CmdErr
            }

            $usesSeparateDllForEventLogMessages = $true
        }
        else {
            # This release uses iotedged.exe as the eventlog message file
            $usesSeparateDllForEventLogMessages = $false
        }

        return $usesSeparateDllForEventLogMessages
    }
    finally {
        Remove-Item "C:\ProgramData\iotedge\iotedged-windows" -Recurse -Force -ErrorAction "SilentlyContinue"
        if ($DeleteArchive) {
            Remove-Item "$ArchivePath" -Recurse -Force -ErrorAction "SilentlyContinue"
        }
    }
}

function Remove-SecurityDaemonResources {
    $success = $true

    $LogKey = "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\iotedged"
    Remove-Item $LogKey -ErrorAction SilentlyContinue -ErrorVariable CmdErr
    Write-Verbose "$(if ($?) { "Deleted registry key '$LogKey'" } else { $CmdErr })"

    $EdgePath = "C:\ProgramData\iotedge"
    $EdgeEventLogMessagesPath = "C:\ProgramData\iotedge-eventlog"

    Remove-Item -Recurse $EdgePath -ErrorAction SilentlyContinue -ErrorVariable CmdErr
    if ($?) {
        Write-Verbose "Deleted install directory '$EdgePath'"
    }
    elseif ($CmdErr.FullyQualifiedErrorId -ne "PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand") {
        Write-Verbose "$CmdErr"
        Write-Host ("Could not delete install directory '$EdgePath'. Please reboot " +
            "your device and run Uninstall-SecurityDaemon again with '-Force'.") `
            -ForegroundColor "Red"
        $success = $false
    }
    else {
        Write-Verbose "$CmdErr"
    }

    Remove-Item -Recurse $EdgeEventLogMessagesPath -ErrorAction SilentlyContinue -ErrorVariable CmdErr
    if ($?) {
        Write-Verbose "Deleted install directory '$EdgeEventLogMessagesPath'"
    }
    elseif ($CmdErr.FullyQualifiedErrorId -ne "PathNotFound,Microsoft.PowerShell.Commands.RemoveItemCommand") {
        Write-Verbose "$CmdErr"
        Write-Warning "Could not delete '$EdgeEventLogMessagesPath'."
        Write-Warning "If you're reinstalling or updating IoT Edge, then this is safe to ignore."
        Write-Warning ("Otherwise, please close Event Viewer, or any PowerShell windows where you ran Get-WinEvent, " +
            "then run Uninstall-SecurityDaemon again with '-Force'.")
    }
    else {
        Write-Verbose "$CmdErr"
    }

    $success
}

function Get-SystemPathKey {
    "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
}

function Get-SystemPath {
    (Get-ItemProperty -Path (Get-SystemPathKey) -Name Path).Path -split ";" | Where-Object {$_.Length -gt 0}
}

function Set-SystemPath {
    $EdgePath = "C:\ProgramData\iotedge"
    $SystemPath = Get-SystemPath
    if ($SystemPath -notcontains $EdgePath) {
        Set-ItemProperty -Path (Get-SystemPathKey) -Name Path -Value (($SystemPath + $EdgePath) -join ";")
        $env:Path += ";$EdgePath"
        Write-Host "Updated system PATH." -ForegroundColor "Green"
    }
    else {
        Write-Host "System PATH does not require an update." -ForegroundColor "Green"
    }
}

function Reset-SystemPath {
    $EdgePath = "C:\ProgramData\iotedge"
    $SystemPath = Get-SystemPath
    if ($SystemPath -contains $EdgePath) {
        $NewPath = $SystemPath | Where-Object { $_ -ne "C:\ProgramData\iotedge" }
        Set-ItemProperty -Path (Get-SystemPathKey) -Name Path -Value ($NewPath -join ";")
        Write-Verbose "Removed IoT Edge directory from system PATH"
    }
}

function Get-VcRuntime {
    if (Test-IotCore) {
        Write-Host "Skipped vcruntime download on IoT Core." -ForegroundColor "Green"
        return
    }

    try {
        Write-Host "Downloading vcruntime." -ForegroundColor "Green"
        Invoke-WebRequest `
            -Uri "https://download.microsoft.com/download/0/6/4/064F84EA-D1DB-4EAA-9A5C-CC2F0FF6A638/vc_redist.x64.exe" `
            -OutFile "$env:TEMP\vc_redist.exe" `
            -UseBasicParsing `
            -Proxy $Proxy
        Invoke-Native "$env:TEMP\vc_redist.exe /quiet /norestart"
        Write-Host "Downloaded vcruntime." -ForegroundColor "Green"
    }
    catch {
        if ($LASTEXITCODE -eq 1638) {
            Write-Host "Skipping vcruntime installation because a newer version is already installed." -ForegroundColor "Green"
        }
        else {
            throw $_
        }
    }
    finally {
        Remove-Item "$env:TEMP\vc_redist.exe" -Force -Recurse -ErrorAction "SilentlyContinue"
    }
}

function Install-IotEdgeService {
    New-Service -Name "iotedge" -BinaryPathName "C:\ProgramData\iotedge\iotedged.exe -c C:\ProgramData\iotedge\config.yaml" | Out-Null
    Start-Service iotedge
    Write-Host "Initialized the IoT Edge service." -ForegroundColor "Green"
}

function Uninstall-IotEdgeService {
    Stop-Service -NoWait -ErrorAction SilentlyContinue -ErrorVariable CmdErr iotedge
    if ($?) {
        Start-Sleep -Seconds 7
        Write-Verbose "Stopped the IoT Edge service"
    }
    else {
        Write-Verbose "$CmdErr"
    }

    if (Invoke-Native "sc.exe delete iotedge" -ErrorAction SilentlyContinue) {
        Write-Verbose "Removed service subkey from the registry"
    }
}

function Add-FirewallExceptions {
    New-NetFirewallRule `
        -DisplayName "iotedged allow inbound 15580,15581" `
        -Direction "Inbound" `
        -Action "Allow" `
        -Protocol "TCP" `
        -LocalPort "15580-15581" `
        -Program "C:\programdata\iotedge\iotedged.exe" `
        -InterfaceType "Any" | Out-Null
    Write-Host "Added firewall exceptions for ports used by the IoT Edge service." -ForegroundColor "Green"
}

function Remove-FirewallExceptions {
    Remove-NetFirewallRule -DisplayName "iotedged allow inbound 15580,15581" -ErrorAction SilentlyContinue -ErrorVariable CmdErr
    Write-Verbose "$(if ($?) { "Removed firewall exceptions" } else { $CmdErr })"
}

function Add-IotEdgeRegistryKey([switch] $UsesSeparateDllForEventLogMessages) {
    if ($UsesSeparateDllForEventLogMessages) {
        $messageFilePath = 'C:\ProgramData\iotedge-eventlog\iotedged_eventlog_messages.dll'
    }
    else {
        $messageFilePath = 'C:\ProgramData\iotedge\iotedged.exe'
    }
    $RegistryContent = @(
        "Windows Registry Editor Version 5.00",
        "",
        "[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\iotedged]"
        "`"CustomSource`"=dword:00000001"
        "`"EventMessageFile`"=`"$($messageFilePath -replace '\\', '\\')`""
        "`"TypesSupported`"=dword:00000007")
    try {
        $RegistryContent | Set-Content "$env:TEMP\iotedge.reg" -Force
        Invoke-Native "reg import $env:TEMP\iotedge.reg"
        Write-Host "Added IoT Edge registry key." -ForegroundColor "Green"
    }
    finally {
        Remove-Item "$env:TEMP\iotedge.reg" -Force -ErrorAction "SilentlyContinue"
    }
}

function Set-ProvisioningMode {
    $ConfigurationYaml = Get-Content "C:\ProgramData\iotedge\config.yaml" -Raw
    if (($Manual) -or ($DeviceConnectionString)) {
        $SelectionRegex = "(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*`".*`"\s*#?\s*device_connection_string:\s*`".*`""
        $ReplacementContent = @(
            "provisioning:",
            "  source: `"manual`"",
            "  device_connection_string: `"$DeviceConnectionString`"")
        ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")) | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force
        Write-Host "Configured device for manual provisioning." -ForegroundColor "Green"
    }
    else {
        $SelectionRegex = "(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*`".*`"\s*#?\s*global_endpoint:\s*`".*`"\s*#?\s*scope_id:\s*`".*`"\s*#?\s*registration_id:\s`".*`""
        $ReplacementContent = @(
            "provisioning:",
            "  source: `"dps`"",
            "  global_endpoint: `"https://global.azure-devices-provisioning.net`"",
            "  scope_id: `"$ScopeId`"",
            "  registration_id: `"$RegistrationId`"")
        $ConfigurationYaml = $ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")

        $SelectionRegex = "(?:[^\S\n]*#[^\S\n]*)?provisioning:\s*#?\s*source:\s*`".*`"\s*#?\s*device_connection_string:\s*`".*`""
        $ReplacementContent = ""
        ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")) | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force

        New-Item "HKLM:\SYSTEM\CurrentControlSet\Services\iotedge" -Force | Out-Null
        New-ItemProperty `
            -Path "HKLM:\SYSTEM\CurrentControlSet\Services\iotedge" `
            -Name "Environment" `
            -PropertyType "MultiString" `
            -Value "IOTEDGE_USE_TPM_DEVICE=ON" `
            -Force | Out-Null

        Write-Host "Configured device for DPS provisioning." -ForegroundColor "Green"
    }
}

function Set-AgentImage {
    if ($AgentImage) {
        $YamlPath = "C:\ProgramData\iotedge\config.yaml"
        $ConfigurationYaml = Get-Content $YamlPath -Raw
        $SelectionRegex = "image:\s*`".*`""
        $ReplacementContent = "image: `"$AgentImage`""
        if ($Username -and $Password) {
            $ConfigurationYaml = $ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")
            $SelectionRegex = "auth:\s*\{\s*\}"
            $AgentRegistry = Get-AgentRegistry
            $Cred = New-Object System.Management.Automation.PSCredential ($Username, $Password)
            $ReplacementContent = @(
                "auth:",
                "      serveraddress: $AgentRegistry",
                "      username: $Username",
                "      password: $($Cred.GetNetworkCredential().Password)")
        }
        ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")) | Set-Content $YamlPath -Force
        Write-Host "Configured device with agent image `"$AgentImage`"." -ForegroundColor "Green"
    }
}

function Set-Hostname {
    $ConfigurationYaml = Get-Content "C:\ProgramData\iotedge\config.yaml" -Raw
    $Hostname = (Invoke-Native "hostname" -Passthru).Trim()
    $SelectionRegex = "hostname:\s*`".*`""
    $ReplacementContent = "hostname: `"$Hostname`""
    ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")) | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force
    Write-Host "Configured device with hostname `"$Hostname`"." -ForegroundColor "Green"
}

function Set-GatewayAddress {
    $ConfigurationYaml = Get-Content "C:\ProgramData\iotedge\config.yaml" -Raw
    if ($ContainerOs -eq "Windows") {
        $GatewayAddress = (Get-NetIpAddress |
                Where-Object {$_.InterfaceAlias -like "*vEthernet (nat)*" -and $_.AddressFamily -like "IPv4"}).IPAddress
    } else {
        $GatewayAddress = (Get-NetIpAddress |
                Where-Object {$_.InterfaceAlias -like "*vEthernet (DockerNAT)*" -and $_.AddressFamily -like "IPv4"}).IPAddress
    }

    $SelectionRegex = "connect:\s*management_uri:\s*`".*`"\s*workload_uri:\s*`".*`""
    $ReplacementContent = @(
        "connect:",
        "  management_uri: `"http://${GatewayAddress}:15580`"",
        "  workload_uri: `"http://${GatewayAddress}:15581`"")
    $ConfigurationYaml = $ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")

    $SelectionRegex = "listen:\s*management_uri:\s*`".*`"\s*workload_uri:\s*`".*`""
    $ReplacementContent = @(
        "listen:",
        "  management_uri: `"http://${GatewayAddress}:15580`"",
        "  workload_uri: `"http://${GatewayAddress}:15581`"")
    $ConfigurationYaml = $ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")

    [Environment]::SetEnvironmentVariable("IOTEDGE_HOST", "http://${GatewayAddress}:15580")
    Invoke-Native "setx /M IOTEDGE_HOST `"http://${GatewayAddress}:15580`""

    $ConfigurationYaml | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force
    Write-Host "Configured device with gateway address `"$GatewayAddress`"." -ForegroundColor "Green"
}

function Set-MobyNetwork {
    $ConfigurationYaml = Get-Content "C:\ProgramData\iotedge\config.yaml" -Raw
    $SelectionRegex = "moby_runtime:\s*uri:\s*`".*`"\s*#?\s*network:\s*`".*`""
    $ReplacementContentWindows = @(
        "moby_runtime:",
        "  uri: `"npipe://./pipe/docker_engine`"",
        "  network: `"nat`"")
    $ReplacementContentLinux = @(
        "moby_runtime:",
        "  uri: `"npipe://./pipe/docker_engine`"",
        "  network: `"azure-iot-edge`"")
    if ($ContainerOs -eq "Windows") {
        ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContentWindows -join "`n")) | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force
        Write-Host "Set the Moby runtime network to nat." -ForegroundColor "Green"
    } else {
        ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContentLinux -join "`n")) | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force
        Write-Host "Set the Moby runtime network to azure-iot-edge." -ForegroundColor "Green"
    }
}

function Get-AgentRegistry {
    $Parts = $AgentImage -split "/"
    if (($Parts.Length -gt 1) -and ($Parts[0] -match "\.")) {
        return $Parts[0]
    }
    return "index.docker.io"
}

function Stop-IotEdgeContainers {
    if (Test-IsDockerRunning 6> $null) {
        Get-RunningContainerNames | `
        Get-ContainersWithLabel -Key "net.azure-devices.edge.owner" -Value "Microsoft.Azure.Devices.Edge.Agent" | `
        Stop-Containers
    }
}

function Get-RunningContainerNames {
    $Names = Invoke-Native "docker ps --format='{{.Names}}'" -Passthru
    return $Names -split {$_ -eq "`r" -or $_ -eq "`n" -or $_ -eq "'"} | where {$_.Length -gt 0}
}

function Get-ContainersWithLabel {
    param(
        [parameter(ValueFromPipeline)]
        $ContainerNames,
        $Key,
        $Value
    )

    process {
        $ContainerNames | Where-Object {
            (Invoke-Native "docker inspect --format=""{{index (.Config.Labels) \""$Key\""}}"" $_" -Passthru).Trim() -eq $Value
        }
    }
}

function Stop-Containers {
    param(
        [parameter(ValueFromPipeline)]
        $Containers
    )

    process {
        $Containers | ForEach-Object {
            Invoke-Native "docker stop $_"
            Write-Verbose "Stopped container $_"
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

Export-ModuleMember -Function Install-SecurityDaemon, Uninstall-SecurityDaemon
}
