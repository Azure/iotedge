# Load alert query objects from 
$Alerts = .\Alert-Queries.ps1

[string[]]$environments = "lh1"
[string[]]$platforms = "Linux-amd64"
# [string[]]$platforms = "Linux-amd64", "Linux-arm32v7"

Write-Host $Alerts
Write-Host $Alerts["MessageRate"]
