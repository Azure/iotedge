Import-Module Az.Monitor

$GreaterThanZero = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "GreaterThan" `
   -Threshold "0"
$LessThanTwo = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "LessThan" `
   -Threshold "2" `

class Alert{
   [ValidateNotNullOrEmpty()][string]$Name
   [ValidateNotNullOrEmpty()][string]$Query
   [ValidateNotNullOrEmpty()]$Comparator #TODO: find way to import type (module manifest?)
}
$Alerts = New-Object System.Collections.Generic.List[Alert]

$LoadGenMessagesPerMinThreshold = 50
$TempFilterMessagesPerMinThreshold = 8 
$UpstreamMessageRateAlertQuery = Get-Content -Path ".\queries\UpstreamMessageRate.kql" 
$UpstreamMessageRateAlertQuery = $UpstreamMessageRateAlertQuery.Replace("<LOADGEN.THRESHOLD>", $LoadGenMessagesPerMinThreshold)
$UpstreamMessageRateAlertQuery = $UpstreamMessageRateAlertQuery.Replace("<TEMPFILTER.THRESHOLD>", $TempFilterMessagesPerMinThreshold)
$UpstreamMessageRate = [Alert]@{
   Name = "upstream-message-rate"
   Query = $UpstreamMessageRateAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($UpstreamMessageRate)

$TempSensorMessagesPerMinThreshold = 8 
$LocalMessageRateAlertQuery = Get-Content -Path ".\queries\LocalMessageRate.kql" 
$LocalMessageRateAlertQuery = $LocalMessageRateAlertQuery.Replace("<TEMPSENSOR.THRESHOLD>", $TempSensorMessagesPerMinThreshold)
$LocalMessageRate = [Alert]@{
   Name = "local-message-rate"
   Query = $LocalMessageRateAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($LocalMessageRate)

$NoUpstreamMessagesQuery = Get-Content -Path ".\queries\NoUpstreamMessages.kql" 
$NoUpstreamMessages  = [Alert]@{
   Name = "no-upstream-messages"
   Query = $NoUpstreamMessagesQuery
   Comparator = $LessThanTwo
}
$Alerts.Add($NoUpstreamMessages)

$NoLocalMessagesQuery = Get-Content -Path ".\queries\NoLocalMessages.kql" 
$NoLocalMessages  = [Alert]@{
   Name = "no-local-messages"
   Query = $NoLocalMessagesQuery
   Comparator = $LessThanTwo
}
$Alerts.Add($NoLocalMessages)

$TwinTesterUpstreamReportedPropertyUpdatesPerMinThreshold = 15 
$ReportedPropertyRateAlertQuery = Get-Content -Path ".\queries\ReportedPropertyRate.kql" 
$ReportedPropertyRateAlertQuery = $ReportedPropertyRateAlertQuery.Replace("<TWINTESTER.THRESHOLD>", $TwinTesterUpstreamReportedPropertyUpdatesPerMinThreshold)
$ReportedPropertyRate = [Alert]@{
   Name = "reported-property-rate"
   Query = $ReportedPropertyRateAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($ReportedPropertyRate)

Write-Output $Alerts
