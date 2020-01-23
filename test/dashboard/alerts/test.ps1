class Alert{
   [ValidateNotNullOrEmpty()][string]$Name
   [ValidateNotNullOrEmpty()][string]$Query
}

$MessageRateAlertName = "message-rate"
$MessageRateAlertQuery = "let environmentPrefix = `"<ENVIRONMENT>`";
let platform = `"<PLATFORM>`";
let loadGenMessageRateThreshold = 70;
let tempFilterMessageRateThreshold = 1;
let mostRecentTestId = toscalar(sanitizedTestMetrics 
| where device contains environmentPrefix and device contains platform
| order by TimeGenerated
| take 1
| project testId);
let filteredLastHour = sanitizedLonghaulMetrics
| where testId == mostRecentTestId
| where TimeGenerated > now() - 2h and TimeGenerated < now() - 1h;
let MessagesSentToUpstreamAtEndTime = filteredLastHour
| project device, moduleName, messageTarget, Name_s, value, TimeGenerated
| where Name_s == `"edgehub_messages_sent_total`"
| where messageTarget == `"upstream`"
| summarize arg_max(TimeGenerated, *) by moduleName
| extend upstreamMessagesEnd = value
| project-away device, messageTarget, TimeGenerated, value, Name_s;
let MessagesSentToUpstreamAtStartTime = filteredLastHour
| project device, moduleName, messageTarget, Name_s, value, TimeGenerated
| where Name_s == `"edgehub_messages_sent_total`"
| where messageTarget == `"upstream`"
| summarize arg_min(TimeGenerated, *) by moduleName
| extend upstreamMessagesStart = value
| project-away device, messageTarget, TimeGenerated, value, Name_s;
let MessagesSentToUpstream = MessagesSentToUpstreamAtEndTime
| join kind = inner(MessagesSentToUpstreamAtStartTime) on moduleName
| project-away moduleName1
| extend upstream_message_rate_per_min = (upstreamMessagesEnd  - upstreamMessagesStart) / 60
| project-away upstreamMessagesEnd, upstreamMessagesStart;
MessagesSentToUpstream
| extend upstream_message_rate_per_min = iff(moduleName contains `"loadGen`", upstream_message_rate_per_min - loadGenMessageRateThreshold, upstream_message_rate_per_min - tempFilterMessageRateThreshold)
| where upstream_message_rate_per_min < 0"

$MessageRateAlert = [Alert]@{
   Name = $MessageRateAlertName
   Query = $MessageRateAlertQuery
}

#####

$AlertName = "<ENVIRONMENT>-<PLATFORM>-<ALERTNAME>"

# for env, for platform, for query type

# substitute alert name information
$env = "lh1"
$platform = "Linux-amd64"

$AlertName = $AlertName.Replace("<ENVIRONMENT>", $env)
$AlertName = $AlertName.Replace("<PLATFORM>", $platform)
$AlertName = $AlertName.Replace("<ALERTNAME>", $MessageRateAlert.Name)
$MessageRateAlert.Query = $MessageRateAlert.Query.Replace("<ENVIRONMENT>", $env)
$MessageRateAlert.Query = $MessageRateAlert.Query.Replace("<PLATFORM>", $platform)

$TriggerCondition = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator "GreaterThan" `
   -Threshold 0 `

$AlertingAction = New-AzScheduledQueryRuleAlertingAction `
   -Severity "3" `
   -Trigger $TriggerCondition `

# inject query
$Source = New-AzScheduledQueryRuleSource `
-Query $MessageRateAlert.Query `
-DataSourceId "/subscriptions/5ed2dcb6-29bb-40de-a855-8c24a8260343/resourceGroups/EdgeBuilds/providers/Microsoft.OperationalInsights/workspaces/iotedgeLogAnalytic" `
-QueryType "ResultCount"

$Schedule = New-AzScheduledQueryRuleSchedule `
-FrequencyInMinutes 15 `
-TimeWindowInMinutes 180 `

New-AzScheduledQueryRule -Location "West US 2" -ResourceGroupName "EdgeBuilds" -Action $AlertingAction -Enabled $true -Description "$env-$platform-longhaul $($MessageRateAlert.Name)" -Schedule $Schedule -Source $Source -Name $AlertName