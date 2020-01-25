class Device{
   [ValidateNotNullOrEmpty()][string]$DeviceId
   [ValidateNotNullOrEmpty()][string]$Environment
   [ValidateNotNullOrEmpty()][string]$Platform
}
$Devices = New-Object System.Collections.Generic.List[Device]

$lh1_linux_amd64 = [Device]@{
   DeviceId = "lh1-Linux-amd64-longhaul"
   Environment = "lh1"
   Platform = "Linux-amd64"
}
$Devices.Add($lh1_linux_amd64)

Write-Output $Devices