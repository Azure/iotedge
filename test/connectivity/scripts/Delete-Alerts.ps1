foreach($line in Get-Content .\alertNames.txt) {
    $line = $line.replace("`n", "")
    if (!$line -eq "")
    {
        Write-Host "Deleting alert: $line"
        Remove-AzScheduledQueryRule -Name $line -ResourceGroupName "EdgeBuilds"
    }
}