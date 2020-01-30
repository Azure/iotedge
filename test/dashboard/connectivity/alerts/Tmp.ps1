$tags = @{module_name = "edgeHub"; quartile = ".9" }

.\Create-Alert.ps1 `
-MetricName "edgeAgent_used_cpu_percent" `
-QueryType "value" `
-QueryComparison ">" `
-QueryTarget 0 `
-Tags $tags `
-TestId "asjkdn" `
-AlertingInterval 15