Param (
    [ValidateNotNullOrEmpty()]
    [string] $TestId = $null,

    [ValidateNotNullOrEmpty()]
    [string] $User = $null,

    [ValidateNotNullOrEmpty()]
    [string] $Password = $null,

    [ValidateNotNullOrEmpty()]
    [string] $Tenant = $null
)

Write-Output "Making Kpi Alerts"
Get-Location

$passwd = ConvertTo-SecureString $Password -AsPlainText -Force
$pscredential = New-Object System.Management.Automation.PSCredential($User, $passwd)
Connect-AzAccount -ServicePrincipal -Credential $pscredential -Tenant $Tenant

$yaml = Get-Content ../a/core-linux/kpis.yaml -Raw

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
