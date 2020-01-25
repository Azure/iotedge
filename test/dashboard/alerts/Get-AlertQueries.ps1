class Alert{
   [ValidateNotNullOrEmpty()][string]$Name
   [ValidateNotNullOrEmpty()][string]$Query
}
$Alerts = New-Object System.Collections.Generic.List[Alert]

$LoadGenMessagesPerMinThreshold = 50
$TempFilterMessagesPerMinThreshold = 8 
$MessageRateAlertQuery = Get-Content -Path ".\queries\MessageRate.kql" 
$MessageRateAlertQuery = $MessageRateAlertQuery.Replace("<LOADGEN.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$MessageRateAlertQuery = $MessageRateAlertQuery.Replace("<TEMPFILTER.THRESHOLD>", $TempFilterMessagesPerMinThreshold)
$MessageRate = [Alert]@{
   Name = "message-rate"
   Query = $MessageRateAlertQuery
}
$Alerts.Add($MessageRate)

$TwinTesterUpstreamReportedPropertyUpdatesPerMinThreshold = 15 
$ReportedPropertyRateAlertQuery = Get-Content -Path ".\queries\ReportedPropertyRate.kql" 
$ReportedPropertyRateAlertQuery = $ReportedPropertyRateAlertQuery.Replace("<TWINTESTER.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$ReportedPropertyRate = [Alert]@{
   Name = "reported-property-rate"
   Query = $ReportedPropertyRateAlertQuery
}
$Alerts.Add($ReportedPropertyRate)

Write-Output $Alerts
