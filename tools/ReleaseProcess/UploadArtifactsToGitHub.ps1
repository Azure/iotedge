$BuildId = "44098637"
$workDir = "C:\Users\yophilav\Downloads\release_test\test\"

function Prepare-DevOps-Artifacts
{
    [CmdletBinding()]
    param (
        <# 
        DevOps BuildID 
        #>
        [Parameter(Mandatory)]
        [string]
        $BuildId,

        <# 
        Absolute path of current working directory
        #>
        [Parameter(Mandatory)]
        [string]
        $workDir
    )

    # Assume Az CLI is installed & logged in.
    $pat = $(az keyvault secret show -n IotEdge1-PAT-msazure --vault-name edgebuildkv | ConvertFrom-Json)
    $pat = $pat.value;

    $encoded64Pat = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(':'+$pat))
    $header = @{
        "Authorization" = "Basic $encoded64Pat"
    }

    # Get all the artifact names
    $response = $(Invoke-WebRequest -Uri "https://dev.azure.com/msazure/One/_apis/build/builds/$BuildId/artifacts?api-version=6.0" -Headers $header)
    $content = ($response.Content | ConvertFrom-Json).value

    foreach ($artifact in $content)
    {
        $artifactName = $artifact.name
        $artifactUrl = $artifact.resource.downloadUrl
        $artifactPath = "$workDir$artifactName"
        $artifactExtension = ".zip"

        # Download and Expand each artifact
        Invoke-WebRequest -Uri $artifactUrl -Headers $header -OutFile "$artifactPath$artifactExtension"
        Expand-Archive -Path "$artifactPath$artifactExtension" -DestinationPath $workDir

        # Each artifact is a directory, fetch the packages within it.
        $packages = $(Get-ChildItem -Path $artifactPath)

        # Within each directory, rename the artifacts
        $component,$os,$suffix = $artifactName.split('-')

        # CentOs7 packages do not need renaming.
        if ($os -eq "centos7")
        {
            echo "Skip renaming :"
            echo $($packages.FullName)
            continue;
        }

        # Ranaming the artifacts
        foreach ($package in $packages)
        {
            echo "Processing : $($package.FullName)"
            $name,$version,$arch,$suffix = $package.Name.split("_")
            $arch,$ext,$suffix = $arch.split(".")

            # Reconstruct the new name from the segments. 
            $finalName = @($name, $version, $os, $arch) -join '_'
            $finalName = "$finalName.$ext"
            $newPath = $(Join-Path -Path "$($package.Directory)" -ChildPath "$finalName")

            # Rename
            Rename-Item -Path $package.FullName -NewName $newPath
        }

        #Remove-Item -Path $artifactPath
    }
}


function Prepare-GitHub-Artifacts
{
    [CmdletBinding()]
    param (
        <# 
        Commit ID
        #>
        [Parameter(Mandatory)]
        [string]
        $CommitId,

        <# 
        Absolute path of current working directory
        #>
        [Parameter(Mandatory)]
        [string]
        $workDir
    )

    # Assume Az CLI is installed & logged in.
    $pat = $(az keyvault secret show -n TestGitHubAccessToken --vault-name edge-e2e-kv | ConvertFrom-Json)
    $pat = $pat.value;

    # Remark: GitHub PAT in KeyVault is already base64. No need to encode it
    $header = @{
        "Accept" = "application/vnd.github.v3+json"
        "Authorization" = "token $pat"
    }

    # Iterate through the runs to find the right run for the artifacts.
    for($page = 1; ; $page++)
    {
        #API Ref Doc: https://docs.github.com/en/rest/reference/actions#workflow-runs
        $url = "https://api.github.com/repos/azure/iot-identity-service/actions/runs?per_page=100&page=$page"
        $actionsRuns = $(Invoke-WebRequest -Headers $header -Uri "$url" | ConvertFrom-JSON)

        if($actionsRuns.Count -eq 0)
        {
            # Searched all pages and could not find artifact for submodule commit.
            Write-Output "GitHub Action runs is exhausted."
            exit 1
        }

        $artifactRun = $actionsRuns.workflow_runs | `
            where {($_.head_sha -eq $CommitId)} | `
            Select-Object -First 1

        if ($artifactRun)
        {
            break;
        }
    }

    # Let's look at the artifacts
    $artifactUrl = $artifactRun.artifacts_url
    $artifacts = $(Invoke-WebRequest -Headers $header -Uri "$artifactUrl" | ConvertFrom-JSON)
    $artifacts = $artifacts.artifacts

    foreach ($artifact in $artifacts)
    {
        
        $downloadUrl = $artifact.archive_download_url
        $artifactName = $artifact.name
        $artifactPath = "$workDir$artifactName"
        $artifactExtension = ".zip"

        Invoke-WebRequest -Headers $header -Uri "$downloadUrl" -OutFile "$artifactPath$artifactExtension"
        Expand-Archive -Path "$artifactPath$artifactExtension" -DestinationPath $artifactPath

        # Within each directory, rename the artifacts
        $component,$os,$suffix = $artifactName.split('_')
    }

}