Param (
    [ValidateNotNullOrEmpty()]
    [int] $TestId = $null
)

$yaml = Get-Content ..\kpis.yaml -Raw

$obj = ConvertFrom-Yaml $yaml
$kpis = $obj["Connectivity"]

foreach ($kpi in $kpis) {
    $kpiName = $kpi.keys
    Write-Output "KPI: $kpiName"
     
    .\Create-Alert.ps1 `
        -MetricName $kpiSettings.metricName `
        -QueryType $kpiSettings.query_type `
        -QueryComparison $kpiSettings.comparison `
        -QueryTarget $kpiSettings.target `
        -KpiName $kpiName `
        -Tags $kpiSettings.tags `
        -TestId $TestId `
        -AlertingInterval 15
}    
