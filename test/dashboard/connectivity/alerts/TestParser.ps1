$yaml = Get-Content C:\Users\Lee\source\repos\edge\and\iotedge\test\dashboard\kpis.yaml -Raw

$obj = ConvertFrom-Yaml $yaml

foreach ($testType in $obj.Keys) {
    echo "Test: $testType"
    $kpis = $obj[$testType]

    foreach ($kpi in $kpis) {
        $kpiName = $kpi.keys
        echo "  Kpi: $kpiName"
       
        $kpiSettings = $kpi.values

        $mn = $kpiSettings.metricName
        echo "    metricName: $mn"

        $kpiSettings.tags
        echo ""
        $fakeTags = @{module_name = "edgeHub"; quartile = .9 }
        $fakeTags
    }    
}

# ConvertTo-Yaml -JsonCompatible $obj
