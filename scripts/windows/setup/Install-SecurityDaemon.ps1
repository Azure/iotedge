<#
 # Installs the IoT Edge Security Daemon on RS4 Windows.
 #>

#requires -Version 5
#requires -RunAsAdministrator

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
    [String] $RegistrationId
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 5

function Install-SecurityDaemon {
    if (-not (Test-IsDockerRunning) -or -not (Test-IsKernelValid)) {
        Write-Host ("`nThe prerequisites for installation of the IoT Edge Security daemon are not met. " +
            "Please fix all known issues before rerunning this script.") `
            -ForegroundColor "Red"
        return
    }

    Get-SecurityDaemon
    Set-Path
    Get-VcRuntime
    Add-FirewallExceptions
    Add-IotEdgeRegistryKey

    Set-ProvisioningMode
    Set-Hostname
    Set-GatewayAddress
    Set-MobyNetwork
    Initialize-IotEdgeService

    Write-Host ("`nThis device is now provisioned with the IoT Edge runtime.`n" +
        "Check the status of the IoT Edge service with `"Get-Service iotedge`"`n" +
        "List running modules with `"iotedge list`"`n" +
        "Display logs from the last five minutes in chronological order with`n" +
        "    Get-WinEvent -ea SilentlyContinue -FilterHashtable @{ProviderName=`"iotedged`";LogName=`"application`";StartTime=[datetime]::Now.AddMinutes(-5)} |`n" +
        "    Select TimeCreated, Message |`n" +
        "    Sort-Object @{Expression=`"TimeCreated`";Descending=`$false}") `
        -ForegroundColor "Green"
}

function Test-IsDockerRunning {
    if ((Get-Service "Docker").Status -eq "Running") {
        Write-Host "Docker is running." -ForegroundColor "Green"
        return $true
    }
    else {
        Write-Host "Docker is not running." -ForegroundColor "Red"
        if ((Get-Item "HKLM:\Software\Microsoft\Windows NT\CurrentVersion").GetValue("EditionID") -eq "IoTUAP") {
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
}

function Test-IsKernelValid {
    $SupportedBuilds = @(17134)
    $CurrentBuild = (Get-Item "HKLM:\Software\Microsoft\Windows NT\CurrentVersion").GetValue("CurrentBuild")
    if ((Invoke-Native "docker info --format {{.Isolation}}" -Passthru) -match "hyperv") {
        Write-Host "Hyper-V isolation is enabled." -ForegroundColor "Green"
        return $true
    }
    else {
        Write-Host "Process isolation is enabled." -ForegroundColor "Green"
        if ($SupportedBuilds -contains $CurrentBuild) {
            Write-Host "The container host is on supported build version $CurrentBuild." -ForegroundColor "Green"
            return $true
        }
        else {
            Write-Host ("The container host is on unsupported build version $CurrentBuild. " +
                "Please use a container host with one of the following supported build versions:`n" +
                ($SupportedBuilds -join "`n")) `
                -ForegroundColor "Red"
            return $false
        }
    }
}

function Get-SecurityDaemon {
    try {
        Invoke-WebRequest `
            -Uri "https://aka.ms/iotedged-windows-latest" `
            -OutFile "$env:TEMP\iotedged-windows.zip" `
            -UseBasicParsing
        Expand-Archive "$env:TEMP\iotedged-windows.zip" "C:\ProgramData\iotedge" -Force
        Copy-Item "C:\ProgramData\iotedge\iotedged-windows\*" "C:\ProgramData\iotedge" -Force
        Write-Host "Downloaded security daemon." -ForegroundColor "Green"
    }
    finally {
        Remove-Item "C:\ProgramData\iotedge\iotedged-windows" -Recurse -Force -ErrorAction "SilentlyContinue"
        Remove-Item "$env:TEMP\iotedged-windows.zip" -Force -ErrorAction "SilentlyContinue"
    }
}

function Set-Path {
    $SystemEnvironment = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
    $EdgePath = "C:\ProgramData\iotedge"
    $SystemPath = (Get-ItemProperty -Path $SystemEnvironment -Name Path).Path
    if ($SystemPath -notlike "*$EdgePath*") {
        Set-ItemProperty -Path $SystemEnvironment -Name Path -Value "$SystemPath;$EdgePath"
        $env:Path += ";$EdgePath"
        Write-Host "Updated system PATH." -ForegroundColor "Green"
    }
    else {
        Write-Host "System PATH does not require an update." -ForegroundColor "Green"
    }
}

function Get-VcRuntime {
    if ((Get-Item "HKLM:\Software\Microsoft\Windows NT\CurrentVersion").GetValue("EditionID") -eq "IoTUAP") {
        Write-Host "Skipped vcruntime download on IoT Core." -ForegroundColor "Green"
        return
    }

    try {
        Invoke-WebRequest `
            -Uri "https://download.microsoft.com/download/0/6/4/064F84EA-D1DB-4EAA-9A5C-CC2F0FF6A638/vc_redist.x64.exe" `
            -OutFile "$env:TEMP\vc_redist.exe" `
            -UseBasicParsing
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

function Initialize-IotEdgeService {
    New-Service -Name "iotedge" -BinaryPathName "C:\ProgramData\iotedge\iotedged.exe -c C:\ProgramData\iotedge\config.yaml" | Out-Null
    Start-Service iotedge
    Write-Host "Initialized the IoT Edge service." -ForegroundColor "Green"
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

function Add-IotEdgeRegistryKey {
    $RegistryContent = @(
        "Windows Registry Editor Version 5.00",
        "",
        "[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\iotedged]"
        "`"CustomSource`"=dword:00000001"
        "`"EventMessageFile`"=`"C:\\ProgramData\\iotedge\\iotedged.exe`""
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
    if ($Manual) {
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
    $GatewayAddress = (Get-NetIpAddress |
            Where-Object {$_.InterfaceAlias -like "*vEthernet (nat)*" -and $_.AddressFamily -like "IPv4"}).IPAddress

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
    $ReplacementContent = @(
        "moby_runtime:",
        "  docker_uri: `"npipe://./pipe/docker_engine`"",
        "  network: `"nat`"")
    ($ConfigurationYaml -replace $SelectionRegex, ($ReplacementContent -join "`n")) | Set-Content "C:\ProgramData\iotedge\config.yaml" -Force
    Write-Host "Set the Moby runtime network to NAT." -ForegroundColor "Green"
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

Install-SecurityDaemon
