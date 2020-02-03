$tags = @{module_name = "edgeHub"; quartile = ".9" }

.\Create-Alert.ps1 `
-MetricName "edgeAgent_used_cpu_percent" `
-QueryType "rate" `
-QueryComparison ">" `
-QueryTarget 0 `
-KpiName "ConnectivityTestAlert" `
-Tags $tags `
-TestId "aaaaa" `
-AlertingInterval 15