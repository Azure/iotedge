class Alert{
   [ValidateNotNullOrEmpty()][string]$Name
   [ValidateNotNullOrEmpty()][string]$Query
}

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
$MessageRate = [Alert]@{
   Name = "message-rate"
   Query = $MessageRateAlertQuery
}

$Alerts = @{ MessageRate = $MessageRateAlert }

Write-Output $Alerts
