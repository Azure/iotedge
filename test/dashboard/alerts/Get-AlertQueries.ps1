Import-Module Az.Monitor

# TODO: clean this up by making a func that returns this object
$GreaterThanZero = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "GreaterThan" `
   -Threshold "0"
$LessThanTwo = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "LessThan" `
   -Threshold "2" 
$LessThanThree = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "LessThan" `
   -Threshold "3" 
$LessThanFourtyTwo = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "LessThan" `
   -Threshold "42" 
$GreaterThanFourtyTwo = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "GreaterThan" `
   -Threshold "42" 

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

$TempSensorMessagesPerMinThreshold = 50 
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

$NoReportedPropertiesQuery = Get-Content -Path ".\queries\NoReportedProperties.kql" 
$NoReportedProperties  = [Alert]@{
   Name = "no-reported-properties"
   Query = $NoReportedPropertiesQuery
   Comparator = $LessThanThree
}
$Alerts.Add($NoReportedProperties)

$QueueLengthThreshold = 1 
$QueueLengthAlertQuery = Get-Content -Path ".\queries\QueueLength.kql" 
$QueueLengthAlertQuery = $QueueLengthAlertQuery.Replace("<QUEUELENGTH.THRESHOLD>", $QueueLengthThreshold)
$QueueLength = [Alert]@{
   Name = "queue-length"
   Query = $QueueLengthAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($QueueLength)

$EdgeAgentCpuThreshold = 100 
$EdgeAgentCpuAlertQuery = Get-Content -Path ".\queries\EdgeAgentCpu.kql" 
$EdgeAgentCpuAlertQuery = $EdgeAgentCpuAlertQuery.Replace("<CPU.THRESHOLD>", $EdgeAgentCpuThreshold)
$EdgeAgentCpu = [Alert]@{
   Name = "edge-agent-cpu"
   Query = $EdgeAgentCpuAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($EdgeAgentCpu)

$EdgeHubCpuThreshold = 100 
$EdgeHubCpuAlertQuery = Get-Content -Path ".\queries\EdgeHubCpu.kql" 
$EdgeHubCpuAlertQuery = $EdgeHubCpuAlertQuery.Replace("<CPU.THRESHOLD>", $EdgeHubCpuThreshold)
$EdgeHubCpu = [Alert]@{
   Name = "edge-hub-cpu"
   Query = $EdgeHubCpuAlertQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($EdgeHubCpu)

$EdgeAgentMemoryThreshold = 100 
$EdgeAgentMemoryQuery = Get-Content -Path ".\queries\EdgeAgentMemory.kql" 
$EdgeAgentMemoryQuery = $EdgeAgentMemoryQuery.Replace("<MEMORY.THRESHOLD>", $EdgeAgentMemoryThreshold)
$EdgeAgentMemory = [Alert]@{
   Name = "edge-agent-memory"
   Query = $EdgeAgentMemoryQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($EdgeAgentMemory)

$EdgeHubMemoryThreshold = 100 
$EdgeHubMemoryQuery = Get-Content -Path ".\queries\EdgeHubMemory.kql" 
$EdgeHubMemoryQuery = $EdgeHubMemoryQuery.Replace("<MEMORY.THRESHOLD>", $EdgeHubMemoryThreshold)
$EdgeHubMemory = [Alert]@{
   Name = "edge-hub-memory"
   Query = $EdgeHubMemoryQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($EdgeHubMemory)

$NumberOfMetricsQuery = Get-Content -Path ".\queries\NumberOfMetrics.kql" 
$NumberOfMetricsTooLow = [Alert]@{
   Name = "number-of-metrics-too-low"
   Query = $NumberOfMetricsQuery
   Comparator = $LessThanFourtyTwo
}
$NumberOfMetricsTooHigh = [Alert]@{
   Name = "number-of-metrics-too-high"
   Query = $NumberOfMetricsQuery
   Comparator = $GreaterThanFourtyTwo
}
$Alerts.Add($NumberOfMetricsTooLow)
$Alerts.Add($NumberOfMetricsTooHigh)

$ModuleStartThreshold = 100 
$ModuleStartsQuery = Get-Content -Path ".\queries\ModuleStarts.kql" 
$ModuleStartsQuery = $ModuleStartsQuery.Replace("<MODULESTARTS.THRESHOLD>", $ModuleStartThreshold)
$ModuleStarts = [Alert]@{
   Name = "failed-module-starts"
   Query = $ModuleStartsQuery
   Comparator = $GreaterThanZero
}
$Alerts.Add($ModuleStarts)

Write-Output $Alerts
