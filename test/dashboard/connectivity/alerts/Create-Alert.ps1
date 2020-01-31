[CmdletBinding()]
Param (
    [ValidateNotNullOrEmpty()]
    [int] $AlertingInterval = $null,

    [ValidateNotNullOrEmpty()]
    [string] $MetricName = $null,

    [ValidateNotNullOrEmpty()]
    [string] $KpiName = $null,

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

if ($QueryType -eq "value")
{
    $ValueQuery = Get-Content -Path ".\queries\value.kql" -Raw
}
else
{
    $ValueQuery = Get-Content -Path ".\queries\rate.kql" -Raw
}

$TagFiltersToAppendToQuery = ""
foreach ($Tag in $Tags.GetEnumerator())
{
    $TagFilter = "";
    if ($Tag.Name -eq "to")
    {
        $TagFilter =  "| where tostring(trim_start(@`"[^/]+/`", extractjson(`"$.to`", tostring(dimensions), typeof(string)))) == $($Tag.Name)"
    }
    else
    {
        $TagFilter = "| where tostring(dimensions.$($Tag.Name)) == `"$($Tag.Value)`""
    }
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
-Action $AlertingAction `
-Enabled $true `
-Description "$TestId $MetricName $QueryComparison $QueryTarget" `
-Schedule $Schedule `
-Source $QuerySource `
-Name $KpiName

#| where tostring(dimensions.target) != "upstream"
#| where tostring(trim_start(@"[^/]+/", extractjson("$.to", tostring(dimensions), typeof(string))))
#| where tostring(dimensions.from) hassuffix "tempSensor2"