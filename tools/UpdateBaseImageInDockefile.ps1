# Define base image version mapping
$baseImageTagMapping=@(
    ("3.1.4-bionic-arm32v7","1.0.5-linux-arm32v7","3.1.5-bionic-arm32v7","1.0.6-linux-arm32v7"), 
    ("3.1.4-bionic-arm64v8","1.0.5-linux-arm64v8","3.1.5-bionic-arm64v8","1.0.6-linux-arm64v8"))

$IoTEdgeRepoRootPath = (Split-Path $PSScriptRoot -Parent)

$LookupSubPath = @("edge-agent", "edge-hub", "edge-modules", "edgelet", "test")

$LookupSubPath.ForEach({
    $LookupPath = Join-Path -Path $IoTEdgeRepoRootPath -ChildPath $_
    
    Get-ChildItem -Path $LookupPath -Filter Dockerfile -Recurse | Foreach-Object {
        ForEach($mapping in $baseImageTagMapping)
        {
            [System.IO.File]::WriteAllText($_.FullName, `
                [System.IO.File]::ReadAllText($_.FullName).`
                Replace($mapping[0], $mapping[2]).`
                Replace($mapping[1], $mapping[3]))
        }
    }
})