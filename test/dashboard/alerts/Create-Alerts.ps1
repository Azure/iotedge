[string[]]$environments = "lh1"
[string[]]$platforms = "Linux-amd64"
$alerts = .\Get-Alert-Queries.ps1 # Load alert query objects from 

$triggerCondition = New-AzScheduledQueryRuleTriggerCondition `
-ThresholdOperator "GreaterThan" `
-Threshold 0 `

$alertingAction = New-AzScheduledQueryRuleAlertingAction `
-Severity "3" `
-Trigger $triggerCondition `

foreach ($env in $environments)
{
    foreach ($platform in $platforms)
    {
        foreach ($alert in $alerts)
        {
            $alertName = "$env-$platform-$($alert.name)"
            $alert.Query = $alert.Query.Replace("<ENVIRONMENT>", $env)
            $alert.Query = $alert.Query.Replace("<PLATFORM>", $platform)

            $schedule = New-AzScheduledQueryRuleSchedule `
            -FrequencyInMinutes 15 `
            -TimeWindowInMinutes 180 `

            $querySource = New-AzScheduledQueryRuleSource `
            -Query $alert.Query `
            -DataSourceId "/subscriptions/5ed2dcb6-29bb-40de-a855-8c24a8260343/resourceGroups/EdgeBuilds/providers/Microsoft.OperationalInsights/workspaces/iotedgeLogAnalytic" `
            -QueryType "ResultCount"

            New-AzScheduledQueryRule -Location "West US 2" -ResourceGroupName "EdgeBuilds" -Action $alertingAction -Enabled $true -Description "$env-$platform-longhaul $($alert.Name)" -Schedule $schedule -Source $querySource -Name $alertName
        }
    }
}
