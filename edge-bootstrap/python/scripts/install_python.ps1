<#
    This script installs embedded python and pip specified by environment
    variable %PythonEmbedVersion%. This is useful when installing python
    within a Windows docker image.
#>

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
    Install Python with pip within a Windows docker image.
#>
    try {
        Invoke-Native "python --help"
    } catch {
        $PythonEmbedVersion = $env:PythonEmbedVersion
        Write-Progress -Activity "Downloading Python version $PythonEmbedVersion"
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
        Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile (Join-Path $env:PYTHONHOME "pip.py")
        Write-Progress -Activity "Downloading pip..." -Completed

        Write-Progress -Activity "Installing pip..."
        Remove-Item (Join-Path $env:PYTHONHOME "python36._pth") -Force
        Invoke-Native "python $env:PYTHONHOME\pip.py"
        Write-Progress -Activity "Installing pip..." -Completed

        # note: this does not seem to take effect when running in a container
        # which is why it is required to be set in the docker file as well
        Invoke-Native "setx /M PATH `"$env:Path`""
        Invoke-Native "setx /M PYTHONHOME `"$env:PYTHONHOME`""
        Invoke-Native "setx /M PYTHONPATH `"$env:PYTHONPATH`""
    }

    Write-Host "Python and pip are installed." -ForegroundColor "Green"
}