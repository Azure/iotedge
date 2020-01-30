$devices = .\Get-Devices.ps1
$alerts = .\Get-AlertQueries.ps1

foreach ($device in $devices)
{
    foreach ($alert in $alerts)
    {
        $alertName = "$($device.deviceId)-$($alert.name)"
        $alert.Query = $alert.Query.Replace("<ENVIRONMENT>", $device.environment)
        $alert.Query = $alert.Query.Replace("<PLATFORM>", $device.platform)

        $triggerCondition = $alert.Comparator

        $alertingAction = New-AzScheduledQueryRuleAlertingAction `
        -Severity "3" `
        -Trigger $triggerCondition `

        $schedule = New-AzScheduledQueryRuleSchedule `
        -FrequencyInMinutes 15 `
        -TimeWindowInMinutes 180 `

        $querySource = New-AzScheduledQueryRuleSource `
        -Query $alert.Query `
        -DataSourceId "/subscriptions/5ed2dcb6-29bb-40de-a855-8c24a8260343/resourceGroups/EdgeBuilds/providers/Microsoft.OperationalInsights/workspaces/iotedgeLogAnalytic" `
        -QueryType "ResultCount"

        New-AzScheduledQueryRule -Location "West US 2" -ResourceGroupName "EdgeBuilds" -Action $alertingAction -Enabled $true -Description "$($device.deviceId) $($alert.Name) $($alert.Threshold)" -Schedule $schedule -Source $querySource -Name $alertName
    }
}

