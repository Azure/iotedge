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

Install-Module -Name Az -AllowClobber -Scope CurrentUser -Force -Verbose
Install-Module powershell-yaml -Verbose

$passwd = ConvertTo-SecureString $Password -AsPlainText -Force
$pscredential = New-Object System.Management.Automation.PSCredential($User, $passwd)
Connect-AzAccount -ServicePrincipal -Credential $pscredential -Tenant $Tenant

$yaml = Get-Content ../a/core-linux/scripts/kpis.yaml -Raw

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
