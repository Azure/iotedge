[CmdletBinding()]
Param (
    [ValidateNotNullOrEmpty()]
    [int] $AlertingInterval = $null,

    [ValidateNotNullOrEmpty()]
    [string] $MetricName = $null,

    [ValidateSet("<",
                 ">",
                 "==",
                 "!=" )]
    [string] $QueryComparison = $null,

    [ValidateSet("GreaterThan",
                 "LessThan",
                 "Equal")]
    [string] $QueryOutputComparison = "GreaterThan",

    [ValidateNotNullOrEmpty()]
    [int] $QueryOutputThreshold = 1,

    [ValidateSet("value",
                 "rate")]
    [string] $QueryType = $null,

    [ValidateNotNullOrEmpty()]
    [string] $QueryTarget = $null,

    [ValidateNotNullOrEmpty()]
    [hashtable] $Tags = $null,

    [ValidateNotNullOrEmpty()]
    [string] $TestId = $null
)

Write-Host "Generating alert for $MetricName $QueryComparison $QueryTarget"

$ValueQuery = Get-Content -Path ".\queries\value.kql" -Raw


# TODO: add/consolidate logic for query type "rate"
$TagNamePlaceholder = "<TAG.NAME>";
$TagValuePlaceholder = "<TAG.VALUE>";
$TagFiltersToAppendToQuery = ""
foreach ($Tag in $Tags.GetEnumerator())
{
    $TagFilter = "| where tostring(dimensions.$TagNamePlaceholder) == `"$TagValuePlaceholder`""
    $TagFilter = $TagFilter.Replace("$TagNamePlaceholder", $Tag.Name)
    $TagFilter = $TagFilter.Replace("$TagValuePlaceholder", $Tag.Value)
    $TagFiltersToAppendToQuery += "`n$TagFilter"
}

$ValueQuery = $ValueQuery.Replace("<TEST.ID>", $TestId)
$ValueQuery = $ValueQuery.Replace("<TARGET>", $QueryTarget)
$ValueQuery = $ValueQuery.Replace("<METRIC.NAME>", $MetricName)
$ValueQuery = $ValueQuery.Replace("<ALERTING.INTERVAL>", $AlertingInterval)
$ValueQuery = $ValueQuery.Replace("<COMPARISON>", $QueryComparison)
$ValueQuery = $ValueQuery.Replace("`n<TAG.FILTER.QUERY>", $TagFiltersToAppendToQuery)

Write-Host "Using alert query: `r`n$ValueQuery"

$TriggerCondition = New-AzScheduledQueryRuleTriggerCondition `
   -ThresholdOperator $QueryOutputComparison `
   -Threshold $QueryOutputThreshold

$AlertingAction = New-AzScheduledQueryRuleAlertingAction `
-Severity "3" `
-Trigger $TriggerCondition `

$Schedule = New-AzScheduledQueryRuleSchedule `
-FrequencyInMinutes $AlertingInterval `
-TimeWindowInMinutes 180 `

$QuerySource = New-AzScheduledQueryRuleSource `
-Query $ValueQuery `
-DataSourceId "/subscriptions/5ed2dcb6-29bb-40de-a855-8c24a8260343/resourceGroups/EdgeBuilds/providers/Microsoft.OperationalInsights/workspaces/iotedgeLogAnalytic" `
-QueryType "ResultCount"

New-AzScheduledQueryRule `
-Location "West US 2" `
-ResourceGroupName "EdgeBuilds" `
-Action $alertingAction `
-Enabled $true `
-Description "$TestId $MetricName $QueryComparison $QueryTarget" `
-Schedule $Schedule `
-Source $querySource `
-Name "ConnectivityTestAlert"