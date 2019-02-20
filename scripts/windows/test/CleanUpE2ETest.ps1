<#
 # Clean up previous E2E test result before running another E2E test.
 #>

#Requires -RunAsAdministrator
 
param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_})]
    [String] $SecurityDaemonInstallScriptPath
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

Write-Host "Prune IoT Edge moby docker system"
try {
  docker -H npipe:////./pipe/iotedge_moby_engine system prune -f
} catch {
  # Ignore this error if moby docker is not installed.
  Write-Verbose "$_"
}

. $SecurityDaemonInstallScriptPath
Write-Host "Uninstall iotedged"
Uninstall-SecurityDaemon -Force

Write-Host "Remove nat VM switch"
Remove-VMSwitch -Force 'nat' -ErrorAction Ignore

Write-Host "Restart Host Network Service"
Restart-Service -name hns
