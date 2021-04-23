#!/bin/pwsh

<#
    .SYNOPSIS
        Downloads the aziot-identity-service package. Run this script from the iotedge repo's root directory.

        GitHub artifacts expire after 90 days. So if this script runs more than 90 days after the aziot submodule's
        commit, it will fail.
#>

Param(
    # Where to save the aziot-identity-service package.
    [Parameter(Mandatory = $true)][string]$DownloadPath,   

    # Name of the GitHub artifact to download. Default: packages_ubuntu-20.04_amd64  
    [string]$ArtifactName = "packages_ubuntu-20.04_amd64",     

    # Filter for the package in the GitHub artifact. Default: aziot-identity-service_*_amd64.deb
    [string]$PackagesFilter = "aziot-identity-service_*_amd64.deb",     

    # Commit of iot-identity-service to use. If not set, the script will try to determine
    # the commit from Cargo.lock.
    [string]$AziotCommit,

    # HTTP proxy and port, if needed.
    [string]$Proxy,

    # Keyvault to query GitHub access token.
    [string]$VaultName = "edge-e2e-kv")

if ($Proxy) {
    Write-Output "Using proxy $Proxy"
    $Proxy = "-Proxy $Proxy"
}
else {
    Write-Output "Downloading without proxy"
}

$AziotCommit = if ($AziotCommit) {
    $AziotCommit
}
else {
    if (!(Test-Path ./edgelet/Cargo.lock)) {
        Write-Output "Cargo.lock for determining aziot-identity-service commit not found."
        Write-Output "Either check out the iotedge repo, or set the aziotis.commit pipeline variable."
        exit 1
    }

    # Any package in the iot-identity-service repo will have the same Git commit hash.
    # So, this script selects the first package in that repo.
    $commitMatches = Select-String -Path ./edgelet/Cargo.lock -Pattern `
        'github.com/Azure/iot-identity-service\?branch=[ -~]+#(?<commit>\w+)' | `
        Select-Object -First 1
    $commit = $commitMatches.Matches.Groups | Where-Object -Property Name -EQ 'commit'

    $commit.Value
}

Write-Output "Getting github token from key vault"

$gitHubToken = Get-AzKeyVaultSecret -VaultName $VaultName -Name "TestGitHubAccessToken" -AsPlainText

Write-Output "Downloading aziot-identity-service $aziot_commit"

$github_headers = '@{"Accept" = "application/vnd.github.v3+json"; "Authorization" = "token ' + $gitHubToken + '"}'

for ($page = 1; ; $page++) {
    $url = "https://api.github.com/repos/azure/iot-identity-service/actions/runs?per_page=100&page=$page"
    Write-Output "GET $url"
    $actions_runs = Invoke-Expression "Invoke-WebRequest $proxy -Headers $github_headers -Uri '$url'" | ConvertFrom-JSON
    $actions_size = $actions_runs.workflow_runs | Measure-Object

    if ($actions_size.Count -eq 0) {
        # Searched all pages and could not find artifact for submodule commit.
        Write-Output "Package for $AziotCommit not found"
        exit 1
    }

    $artifacts_link = $actions_runs.workflow_runs | `
        Where-Object { ($_.head_sha -eq $AziotCommit) -and ($_.name -eq 'packages') } | `
        Select-Object -First 1 -ExpandProperty artifacts_url

    if ([string]::IsNullOrEmpty($artifacts_link)) {
        # Artifact not on this page.
        continue
    }

    Write-Output "GET $artifacts_link"
    $artifacts = Invoke-Expression "Invoke-WebRequest $proxy -Headers $github_headers -Uri '$artifacts_link'" | ConvertFrom-JSON
    $download_link = $artifacts.artifacts | Where-Object { $_.name -eq $ArtifactName } | `
        Select-Object -ExpandProperty archive_download_url

    Write-Output "GET $download_link"
    Invoke-Expression "Invoke-WebRequest $proxy -Headers $github_headers -Uri '$download_link' -OutFile aziot-identity-service.zip"

    Write-Output "Extract aziot-identity-service.zip"
    Expand-Archive -Path aziot-identity-service.zip -DestinationPath aziot-identity-service -Force

    $packages = Get-ChildItem -Recurse aziot-identity-service -Filter $PackagesFilter
    $packagePath = Convert-Path $DownloadPath

    Write-Output "Copy $packages to $packagePath"
    Copy-Item $packages -Destination $packagePath

    Write-Output "Cleaning up..."
    Remove-Item aziot-identity-service -Recurse

    Write-Output "Done"
    exit 0
}
