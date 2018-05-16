& {
$ErrorActionPreference = "Stop"
Set-StrictMode -Version 5

function Get-WindowsBuild {
    (Get-Item "HKLM:\Software\Microsoft\Windows NT\CurrentVersion").GetValue("CurrentBuild")
}

function Get-WindowsEdition {
    (Get-Item "HKLM:\Software\Microsoft\Windows NT\CurrentVersion").GetValue("EditionID")
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
        } elseif ($Passthru) {
            $out
        }
    }
}

$WindowsBuild = 16299
$RequiredFeatures = "Microsoft-Hyper-V", "Containers"
$DockerVersion = "17.09"
$PythonEmbedVersion = "3.6.3"

if ((Get-WindowsEdition) -ne "IoTUAP") {
    $dockerHelp = "docker-for-windows/install/"
}


<#
    Verify that the script is running as an administrator
#>

$CurrentUser = New-Object `
    -TypeName Security.Principal.WindowsPrincipal `
    -ArgumentList $([Security.Principal.WindowsIdentity]::GetCurrent())

if (-not $CurrentUser.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
    Write-Host ("This script must be run as an administrator. " +
        "Please rerun this script in a new PowerShell session running as an administrator.") `
        -ForegroundColor "Red"
    return
}

<#
    Verify that the system's Windows build version is correct.
#>

if ((Get-WindowsBuild) -ne $WindowsBuild) {
    Write-Host ("Azure IoT Edge on Windows requires Windows Fall Creators Update a.k.a. RS3 (build $WindowsBuild). " +
        "The current Windows build is $(Get-WindowsBuild). " + 
        "Please ensure that the current Windows build is $WindowsBuild to run Azure IoT Edge with Windows containers.") `
        -ForegroundColor "Red"
    return
}

Write-Host "The current Windows build is $(Get-WindowsBuild)." -ForegroundColor "Green"

<#
    Ensure that Windows optional features required by Azure IoT Edge are enabled. These features are neither present
    nor needed on Windows IoT Core, so this step is skipped on that platform.
#>

if ((Get-WindowsEdition) -ne "IoTUAP") {
    Write-Progress -Activity "Enabling required features..."
    try {
        if ((Enable-WindowsOptionalFeature -FeatureName $RequiredFeatures -Online -All -NoRestart).RestartNeeded) {
            Write-Host ("A restart is required before completing the remainder of the installation. " +
                "This script must be rerun after the system has been restarted. " +              
                "Would you like to restart now? (Y/N)") `
                -ForegroundColor "White"

            $response = Read-Host
            while(@("Y", "N") -notcontains $response) {
                Write-Host "Please enter Y or N to indicate whether the system should be restarted." `
                    -ForegroundColor "Yellow"
                $response = Read-Host
            }
           
            if ($response -eq "Y") {
                Restart-Computer -Confirm:$false
            }
            return
        }
    } catch {
        Write-Host ("Unable to enable a required Windows feature for Azure IoT Edge. " +
            "If the current system is running in a virtual machine, " +
            "then please ensure that nested virtualization is enabled. See " +
            "https://docs.microsoft.com/en-us/virtualization/hyper-v-on-windows/user-guide/nested-virtualization " +
            "for more information.") `
            -ForegroundColor "Red"
        return
    }
    Write-Progress -Activity "Enabling required features..." -Completed
    
    Write-Host "Required Windows features enabled successfully." -ForegroundColor "Green"
}

<#
    Verify Docker version
#>

try {
    $ver, $os = (Invoke-Native "docker version --format {{.Server.Version}},{{.Server.Os}}" -Passthru) -split ","
} catch {
    <#
        Try to switch containers and docker version one more time before bailing. Sometimes the daemon can't start with
        Linux containers due to insufficient memory, but works with Windows containers
    #>
    if ((Get-WindowsEdition) -ne "IoTUAP") {    
        try {
            Invoke-Native "`"$env:ProgramFiles\Docker\Docker\DockerCli.exe`" -SwitchDaemon"
            $ver, $os = (Invoke-Native "docker version --format {{.Server.Version}},{{.Server.Os}}" -Passthru) -split ","
        } catch {
            Write-Host ("No existing Docker installation found. " +
                "Please visit https://docs.docker.com/$dockerHelp to install Docker for Windows " +
                "version $DockerVersion or greater.") `
                -ForegroundColor "Red"
            return
        }
    } else {
        Write-Host ("No Docker installation found. " +
            "Please complete Step 4 from instructions at https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-core") `
            -ForegroundColor "Red"
        return
    }
}

if ((Get-WindowsEdition) -ne "IoTUAP") {
    if ($ver -lt $DockerVersion) {
        Write-Host ("Existing Docker installation with version $ver found. " +
            "Please visit https://docs.docker.com/$dockerHelp to install Docker for Windows " +
            "version $DockerVersion or greater.") `
            -ForegroundColor "Red"
        return
    }
}

Write-Host "The current Docker version is $ver." -ForegroundColor "Green"

<#
    Enable Windows containers in Docker on Windows IoT Enterprise. Windows IoT Core does not support fully virtualized
    containers, so this step is skipped on that platform.
#>

if ((Get-WindowsEdition -ne "IoTUAP") -and (-not ($os -match "\s*windows\s*$"))) {
    Write-Host "Docker is currently not running Windows containers. Windows containers will be enabled now." `
        -ForegroundColor "Yellow"

    Write-Progress -Activity "Enabling Windows containers in Docker..."
    try {
        Invoke-Native "`"$env:ProgramFiles\Docker\Docker\DockerCli.exe`" -SwitchDaemon"
        $os = Invoke-Native "docker version --format {{.Server.Os}}" -Passthru
        if (-not ($os -match "\s*windows\s*$")) {
            throw "Unable to switch to Windows containers."
        }
    } catch {
        Write-Host ("Unable to switch to Windows containers. " +
            "Please rerun this script after switching to Windows containers.") `
            -ForegroundColor "Red"
        return
    }
    Write-Progress -Activity "Enabling Windows containers in Docker..." -Completed
}

Write-Host "Docker is running Windows containers." -ForegroundColor "Green"

<#
    Install Python with pip on Windows IoT Core.
#>

if ((Get-WindowsEdition) -eq "IoTUAP") {
    try {
        Invoke-Native "python --help"
    } catch {
        Write-Progress -Activity "Downloading Python..."
        Invoke-WebRequest `
            -Uri "https://www.python.org/ftp/python/$PythonEmbedVersion/python-$PythonEmbedVersion-embed-amd64.zip" `
            -OutFile (Join-Path $env:TEMP "py.zip")
        Write-Progress -Activity "Downloading Python..." -Completed
        
        Write-Progress -Activity "Installing Python..."
        try {
            $env:PYTHONHOME = Join-Path $env:ProgramData "pyiotedge"
            @($env:PYTHONHOME, "$env:PYTHONHOME\scripts") | 
                ForEach-Object {$env:PATH = $env:PATH -replace "$([Regex]::Escape($_))(;|$)", ""}
            $env:PATH += ";$env:PYTHONHOME;$env:PYTHONHOME\scripts"
            $env:PYTHONPATH = "$env:PYTHONHOME\python36;$env:PYTHONHOME\Lib;$env:PYTHONHOME\Lib\site-packages;"  
            Expand-Archive (Join-Path $env:TEMP "py.zip") $env:PYTHONHOME -Force
            Expand-Archive (Join-Path $env:PYTHONHOME "python36.zip") (Join-Path $env:PYTHONHOME "python36") -Force
        } finally {
            Remove-Item (Join-Path $env:TEMP "py.zip") -Recurse -Force -ErrorAction "SilentlyContinue"
            Remove-Item (Join-Path $env:TEMP "python36.zip") -Recurse -Force -ErrorAction "SilentlyContinue"
        }
        Write-Progress -Activity "Installing Python..." -Completed
        
        Write-Progress -Activity "Downloading pip..."
        Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile (Join-Path $env:PYTHONHOME "dep-bootstrapper.py")
        Write-Progress -Activity "Downloading pip..." -Completed
        
        Write-Progress -Activity "Installing pip..."
        Remove-Item (Join-Path $env:PYTHONHOME "python36._pth") -Force
        Invoke-Native "python $env:PYTHONHOME\dep-bootstrapper.py"
        Write-Progress -Activity "Installing pip..." -Completed

        if ((Get-WindowsEdition) -ne "IoTUAP") {
            [Environment]::SetEnvironmentVariable(
                "Path", $env:Path, [System.EnvironmentVariableTarget]::Machine)
            [Environment]::SetEnvironmentVariable(
                "PYTHONHOME", $env:PYTHONHOME, [System.EnvironmentVariableTarget]::Machine)
            [Environment]::SetEnvironmentVariable(
                "PYTHONPATH", $env:PYTHONPATH, [System.EnvironmentVariableTarget]::Machine)
        } else {
            Invoke-Native "setx /M PATH `"$env:Path`""
            Invoke-Native "setx /M PYTHONHOME `"$env:PYTHONHOME`""
            Invoke-Native "setx /M PYTHONPATH `"$env:PYTHONPATH`""
        }
    }

    Write-Host "Python and pip are installed." -ForegroundColor "Green"
}

<#
    Address iotedgectl installation
#>

if ((Get-WindowsEdition) -eq "IoTUAP") {
    Write-Progress -Activity "Installing iotedgectl..."   
    Invoke-Native "pip install -U azure-iot-edge-runtime-ctl"
    Write-Progress -Activity "Installing iotedgectl..." -Completed

    Write-Host "iotedgectl installed successfully." -ForegroundColor "Green"
    Write-Host "'iotedgectl --help' shows usage." -ForegroundColor "Green"
} else {
    Write-Host ("`nThis system meets all prerequisites to run Azure IoT Edge with Windows containers. " +
        "Please ensure that the latest version of Python 2.7 is installed (see https://www.python.org/downloads/) " +
        "and run 'pip install -U azure-iot-edge-runtime-ctl' to install iotedgectl.`n") `
        -ForegroundColor "Green"
}
}
