<#
 # Deploy iotedgectl on the current device.
 #>

param (
    [Parameter(Mandatory = $True)]
    [ValidateScript( {Test-Path $_})]
    [String]$Archive,

    [Switch]$Clean
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$ExtractionPath = Join-Path $Env:TEMP "edgectl"
$OldPath = $Env:PATH
$Env:PATH += ";C:\Data\ProgramData\pyiotedge;C:\Data\ProgramData\pyiotedge\scripts;c:\python27\scripts"

Write-Host "Installing iotedgectl."
try {
    Expand-Archive $Archive $ExtractionPath -Force

    Push-Location (Join-Path $ExtractionPath "azure-iot-edge-runtime-ctl-*")
    pip install -U .
    Pop-Location
}
finally {
    $Env:PATH = $OldPath
    if ($Clean) {
        Remove-Item $Archive -Force
        Remove-Item $ExtractionPath -Force -Recurse
    }
}
