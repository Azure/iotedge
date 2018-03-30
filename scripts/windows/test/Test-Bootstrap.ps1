<#
 # Runs all the iotedgectl unit and integration tests. Requires docker, python, and pip.
 #>

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$TestCommand = [IO.Path]::Combine(
    $PSScriptRoot,
    "..",
    "..",
    "..",
    "edge-bootstrap",
    "python",
    "scripts",
    "run_docker_image_tests.bat")

Write-Host "Running iotedgectl tests: $TestCommand"
Invoke-Expression $TestCommand
if ($LASTEXITCODE -ne 0) {
    throw "Failed running iotedgectl tests"
}
