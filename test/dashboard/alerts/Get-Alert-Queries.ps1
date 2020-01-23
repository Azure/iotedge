class Alert{
   [ValidateNotNullOrEmpty()][string]$Name
   [ValidateNotNullOrEmpty()][string]$Query
}
$Alerts = New-Object System.Collections.Generic.List[Alert]

$LoadGenMessagesPerMinThreshold = 50
$TempFilterMessagesPerMinThreshold = 8 
$MessageRateAlertQuery = Get-Content -Path ".\queries\MessageRate.txt" 
$MessageRateAlertQuery = $MessageRateAlertQuery.Replace("<LOADGEN.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$MessageRateAlertQuery = $MessageRateAlertQuery.Replace("<TEMPFILTER.THRESHOLD>", $TempFilterMessagesPerMinThreshold)
$MessageRate = [Alert]@{
   Name = "message-rate"
   Query = $MessageRateAlertQuery
}
$Alerts.Add($MessageRate)

Write-Output $Alerts
