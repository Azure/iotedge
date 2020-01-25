Import-Module Az.Monitor

$GreaterThanZero = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "GreaterThan" `
   -Threshold "0"
$EqualToZero = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "Equal" `
   -Threshold "0" `

class Alert{
   [ValidateNotNullOrEmpty()][string]$Name
   [ValidateNotNullOrEmpty()][string]$Query
   [ValidateNotNullOrEmpty()]$Comparator #TODO: find way to import type
}
$Alerts = New-Object System.Collections.Generic.List[Alert]

$LoadGenMessagesPerMinThreshold = 50
$TempFilterMessagesPerMinThreshold = 8 
$MessageRateAlertQuery = Get-Content -Path ".\queries\UpstreamMessageRate.kql" 
$MessageRateAlertQuery = $MessageRateAlertQuery.Replace("<LOADGEN.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$MessageRateAlertQuery = $MessageRateAlertQuery.Replace("<TEMPFILTER.THRESHOLD>", $TempFilterMessagesPerMinThreshold)
$MessageRate = [Alert]@{
   Name = "message-rate"
   Query = $MessageRateAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($MessageRate)

$TwinTesterUpstreamReportedPropertyUpdatesPerMinThreshold = 15 
$ReportedPropertyRateAlertQuery = Get-Content -Path ".\queries\ReportedPropertyRate.kql" 
$ReportedPropertyRateAlertQuery = $ReportedPropertyRateAlertQuery.Replace("<TWINTESTER.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$ReportedPropertyRate = [Alert]@{
   Name = "reported-property-rate"
   Query = $ReportedPropertyRateAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($ReportedPropertyRate)

$NoUpstreamMessagesQuery = Get-Content -Path ".\queries\NoUpstreamMessages.kql" 
$NoUpstreamMessagesQuery = $ReportedPropertyRateAlertQuery.Replace("<TWINTESTER.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$NoUpstreamMessages  = [Alert]@{
   Name = "reported-property-rate"
   Query = $ReportedPropertyRateAlertQuery
   Comparator = $EqualToZero
}
$Alerts.Add($NoUpstreamMessages)

Write-Output $Alerts
