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

$lh1_linux_arm32v7 = [Device]@{
   DeviceId = "lh1-Linux-arm32v7-longhaul"
   Environment = "lh1"
   Platform = "Linux-arm32v7"
}
$Devices.Add($lh1_linux_arm32v7)

$lh1_linux_arm64v8 = [Device]@{
   DeviceId = "lh1-Linux-arm64v8-longhaul"
   Environment = "lh1"
   Platform = "Linux-arm64v8"
}
$Devices.Add($lh1_linux_arm64v8)

$lh1_windows_amd64 = [Device]@{
   DeviceId = "winpro-lh1-Windows-x64-longhaul"
   Environment = "lh1"
   Platform = "Windows-x64"
}
$Devices.Add($lh1_linux_arm64v8)

$lh2_linux_amd64 = [Device]@{
   DeviceId = "lh2-Linux-amd64-longhaul"
   Environment = "lh2"
   Platform = "Linux-amd64"
}
$Devices.Add($lh2_linux_amd64)

$lh2_linux_arm32v7 = [Device]@{
   DeviceId = "lh2-Linux-arm32v7-longhaul"
   Environment = "lh2"
   Platform = "Linux-arm32v7"
}
$Devices.Add($lh2_linux_arm32v7)

$lh2_linux_arm64v8 = [Device]@{
   DeviceId = "lh2-Linux-arm64v8-longhaul"
   Environment = "lh2"
   Platform = "Linux-arm64v8"
}
$Devices.Add($lh2_linux_arm64v8)

$lh2_windows_amd64 = [Device]@{
   DeviceId = "winpro-lh2-Windows-x64-longhaul"
   Environment = "lh2"
   Platform = "Windows-x64"
}
$Devices.Add($lh2_linux_arm64v8)

$lh3_linux_amd64 = [Device]@{
   DeviceId = "lh3-Linux-amd64-longhaul"
   Environment = "lh3"
   Platform = "Linux-amd64"
}
$Devices.Add($lh3_linux_amd64)

$lh3_linux_arm32v7 = [Device]@{
   DeviceId = "lh3-Linux-arm32v7-longhaul"
   Environment = "lh3"
   Platform = "Linux-arm32v7"
}
$Devices.Add($lh3_linux_arm32v7)

$lh3_linux_arm64v8 = [Device]@{
   DeviceId = "lh3-Linux-arm64v8-longhaul"
   Environment = "lh3"
   Platform = "Linux-arm64v8"
}
$Devices.Add($lh3_linux_arm64v8)

$lh3_windows_amd64 = [Device]@{
   DeviceId = "winpro-lh3-Windows-x64-longhaul"
   Environment = "lh3"
   Platform = "Windows-x64"
}
$Devices.Add($lh3_linux_arm64v8)

Write-Output $Devices