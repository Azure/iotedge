$BuildId = "44098637"
$workDir = "C:\Users\yophilav\Downloads\release_test\test1\"

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

    $artifactFinalists = @();
    $workDir = "$(Join-Path -Path $workDir -ChildPath 'IE')\"
    New-Item -ItemType Directory -Force -Path $workDir

    foreach ($artifact in $content)
    {
        $artifactName = $artifact.name
        $artifactUrl = $artifact.resource.downloadUrl
        $artifactPath = "$workDir$artifactName"
        $artifactExtension = ".zip"

        # Download and Expand each artifact
        Retry-Command -ScriptBlock {
            Invoke-WebRequest -Uri $artifactUrl -Headers $header -OutFile "$artifactPath$artifactExtension" | Out-Null
            Expand-Archive -Path "$artifactPath$artifactExtension" -DestinationPath $workDir
        }

        # Each artifact is a directory, fetch the packages within it.
        $packages = $(Get-ChildItem -Path $artifactPath/* -Recurse `
            -Include "*.deb", "*.rpm" `
            -Exclude "*.src*", "*dev*", "*dbg*" `
            | where { ! $_.PSIsContainer })

        # Within each directory, rename the artifacts
        $component,$os,$suffix = $artifactName.split('-')

        # CentOs7 packages do not need renaming.
        if ($os -eq "centos7")
        {
            echo "Skip renaming :"
            echo $($packages.FullName)
            $artifactFinalists += $packages;
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

            # Record renamed files
            $artifactFinalists += $(Get-Item $newPath)
        }

        # TODO: Clean up
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

    $workDir = "$(Join-Path -Path $workDir -ChildPath 'IIS')\"
    New-Item -ItemType Directory -Force -Path $workDir

    # Let's look at the artifacts
    $artifactUrl = $artifactRun.artifacts_url
    $artifacts = $(Invoke-WebRequest -Headers $header -Uri "$artifactUrl" | ConvertFrom-JSON)
    $artifacts = $artifacts.artifacts
    $artifactFinalists = @();

    foreach ($artifact in $artifacts)
    {
        $downloadUrl = $artifact.archive_download_url
        $artifactName = $artifact.name
        $artifactPath = "$workDir$artifactName"
        $artifactExtension = ".zip"

        echo "Downloading $artifactName"
        Retry-Command -ScriptBlock {
            Invoke-WebRequest -Headers $header -Uri "$downloadUrl" -OutFile "$artifactPath$artifactExtension"
            Expand-Archive -Path "$artifactPath$artifactExtension" -DestinationPath $artifactPath
        }

        # Each artifact is a directory, let's get only packages in them.
        $packages = $(Get-ChildItem -Path $artifactPath/* -Recurse `
            -Include "*.deb", "*.rpm" `
            -Exclude "*.src*", "*dev*", "*dbg*" `
            | where { ! $_.PSIsContainer })

        # Within each directory, rename the artifacts (i.e. "packages_debian-10-slim_aarch64")
        $component,$os,$suffix = $artifactName.split('_')
        $osName,$osVersion,$osType,$suffix = $os.split('-')
        $os = @($osName,$osVersion) -join ''

        # CentOs7 packages do not need renaming.
        if ($os -eq "centos7")
        {
            echo "Skip renaming :"
            echo $artifactPath
            $artifactFinalists += $packages;
            continue;
        }

        # Renaming
        foreach ($package in $packages)
        {
            echo "Processing : $($package.FullName)"
            # Deconstruct the artifact name
            $name,$version,$arch,$suffix = $package.Name.split("_")
            $arch,$ext,$suffix = $arch.split(".")

            # Reconstruct the new name from the segments.
            $finalName = @($name, $version, $os, $arch) -join '_'
            $finalName = "$finalName.$ext"
            $newPath = $(Join-Path -Path "$($package.Directory)" -ChildPath "$finalName")

            # Rename
            Rename-Item -Path $package.FullName -NewName $newPath

            # Record renamed files
            $artifactFinalists += $(Get-Item $newPath)
        }
    }

    # To be uploaded
    $artifactFinalists

    # https://docs.github.com/en/rest/reference/repos#upload-a-release-asset
    # https://docs.github.com/en/rest/reference/repos#create-a-release

    # TODO: Clean up
}


# Referred from https://stackoverflow.com/questions/45470999/powershell-try-catch-and-retry
function Retry-Command {
    [CmdletBinding()]
    Param(
        [Parameter(Position=0, Mandatory=$true)]
        [scriptblock]$ScriptBlock,

        [Parameter(Position=1, Mandatory=$false)]
        [int]$Maximum = 5,

        [Parameter(Position=2, Mandatory=$false)]
        [int]$Delay = 100
    )

    Begin {
        $cnt = 0
    }

    Process {
        do {
            $cnt++
            try {
                $ScriptBlock.Invoke()
                return
            } catch {
                Write-Error $_.Exception.InnerException.Message -ErrorAction Continue
                Start-Sleep -Milliseconds $Delay
            }
        } while ($cnt -lt $Maximum)

        # Throw an error after $Maximum unsuccessful invocations. Doesn't need
        # a condition, since the function returns upon successful invocation.
        throw 'Execution failed.'
    }
}