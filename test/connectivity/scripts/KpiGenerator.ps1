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

$scriptsDirectory = "../a/core-linux/scripts"
# $scriptsDirectory = "C:/Users/Lee/source/repos/edge/and/iotedge/test/connectivity/scripts"

Write-Output "Making Kpi Alerts"
Get-Location

if (-Not $(Get-InstalledModule -Name Az)) {
    Install-Module -Name Az -AllowClobber -Scope CurrentUser -Force -Verbose
}
if (-Not $(Get-InstalledModule -Name powershell-yaml)) {
    Install-Module -Name powershell-yaml -Force -Verbose
}

$passwd = ConvertTo-SecureString $Password -AsPlainText -Force
$pscredential = New-Object System.Management.Automation.PSCredential($User, $passwd)
Clear-AzContext -Force -Verbose
Connect-AzAccount -ServicePrincipal -Credential $pscredential -Tenant $Tenant -Verbose
Set-AzContext -Subscription "IOT_EDGE_DEV1" -Verbose

$yaml = Get-Content "$scriptsDirectory/kpis.yaml" -Raw

$obj = ConvertFrom-Yaml $yaml
$kpis = $obj["Connectivity"]

foreach ($kpi in $kpis) {
    $kpiName = $kpi.keys
    Write-Output "KPI: $kpiName"

    $kpiSettings = $kpi.values

    & "$scriptsDirectory/Create-Alert.ps1" `
        -MetricName $kpiSettings.metricName `
        -QueryType $kpiSettings.query_type `
        -QueryComparison $kpiSettings.comparison `
        -QueryTarget $kpiSettings.target `
        -KpiName $kpiName `
        -Tags $kpiSettings.tags `
        -TestId $TestId `
        -AlertingInterval 15 `
        -scriptsDirectory $scriptsDirectory
}    
