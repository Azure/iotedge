## Copyright (c) Microsoft. All rights reserved.
## Licensed under the MIT license. See LICENSE file in the project root for full license information.

<#
    .SYNOPSIS
        This function downloads and prepares the Iot Edge Daemon artifacts from DevOps pipeline for a release. 
        The final artifacts are collected under "<WorkDir>/output" path.
    .DESCRIPTION
        1) Download edgelet artifacts from a given BuildId from a DevOps pipeline
        2) Extract the *.zip artifacts into their own directories
        3) Traverse the directories and rename a relavant artifact into the following format: 
              .DEB _________________ {component}_{version}_{os}_{arch}.deb 
              .RPM _________________ {component}-{version}.{os}.{arch}.rpm  (No name change)
        4) Move the renamed artifacts to a staging directory at "<WorkDir>/output"
    .INPUTS 
        $DevOpsPat ____________ (String) Environment variable contains DevOps Personal Access Token (with Build:Read privilege) in UTF8 encoding
    .PARAMETER BuildId
        DevOps build ID for the downloading artifacts
    .PARAMETER WorkDir
        Absolute path of the working directory
#>
function Prepare-DevOps-Artifacts
{
    [CmdletBinding()]
    param (
        <# 
        DevOps build ID for the downloading artifacts
        #>
        [Parameter(Mandatory)]
        [string]
        $BuildId,

        <# 
        Absolute path of the working directory
        #>
        [Parameter(Mandatory)]
        [string]
        $WorkDir
    )

    if ([string]::IsNullOrEmpty($DevOpsPat))
    {
        echo 'Environment variable ($DevOpsPat) is required.'
        return 1;
    }
    else
    {
        $pat = $DevOpsPat;
    }

    $encoded64Pat = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(':'+$pat))
    $header = @{
        "Authorization" = "Basic $encoded64Pat"
    }

    # Get all the artifact names
    $response = $(Invoke-WebRequest -Uri "https://dev.azure.com/msazure/One/_apis/build/builds/$BuildId/artifacts?api-version=6.0" -Headers $header)
    $content = ($response.Content | ConvertFrom-Json).value

    $outputDir = "$(Join-Path -Path $WorkDir -ChildPath 'output')\"
    New-Item -ItemType Directory -Force -Path $outputDir
    $WorkDir = "$(Join-Path -Path $WorkDir -ChildPath 'IE')\"
    New-Item -ItemType Directory -Force -Path $WorkDir

    $artifactFinalists = @();
    $artifactExtension = ".zip"

    foreach ($artifact in $content)
    {
        $artifactName = $artifact.name
        $artifactUrl = $artifact.resource.downloadUrl
        $artifactPath = "$WorkDir$artifactName"

        # Download and Expand each artifact
        Retry-Command -ScriptBlock {
            echo "Downloading $artifactName"
            Invoke-WebRequest -Uri $artifactUrl -Headers $header -OutFile "$artifactPath$artifactExtension" | Out-Null
            Expand-Archive -Path "$artifactPath$artifactExtension" -DestinationPath $WorkDir -Force
        }

        # Each artifact is a directory, fetch the packages within it.
        $packages = $(Get-ChildItem -Path $artifactPath/* -Recurse `
            -Include "*.deb", "*.rpm" `
            -Exclude "*.src*", "*dev*", "*dbg*", "*debug*" `
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

        echo ""
    }

    echo ""

    # Stage uploading files
    foreach ($artifact in $artifactFinalists)
    {
        echo "Moving : $($artifact.FullName)"
        echo "To : $(Join-Path -Path $outputDir -ChildPath $artifact.Name)"
        Move-Item -Path $artifact.FullName -Destination $(Join-Path -Path $outputDir -ChildPath $artifact.Name) -Force
    }
}


<#
    .SYNOPSIS
        This function downloads and prepares the IoT Identity Service artifacts from Github Action for a release.
        The final artifacts are collected under "<WorkDir>/output" path.
    .DESCRIPTION
        1) Download edgelet artifacts from a given commit off of the Github build action:
              https://github.com/Azure/iot-identity-service/actions/workflows/packages.yaml
        2) Extract the *.zip artifacts into their own directories
        3) Traverse the directories and rename a relavant artifact into the following format: 
              .DEB _________________ {component}_{version}_{os}_{arch}.deb 
              .RPM _________________ {component}-{version}.{os}.{arch}.rpm  (No name change)
        4) Move the renamed artifacts to a staging directory at "<WorkDir>/output"
    .INPUTS 
        $GitHubPat ____________ (String) Environment variable contains BASE64 Github Personal Access Token (with Repos:Read&Write privilege) in UTF8 encoding
    .PARAMETER CommitId
        Github SHA1 commit id
    .PARAMETER WorkDir
        Absolute path of the working directory
#>
function Prepare-GitHub-Artifacts
{
    [CmdletBinding()]
    param (
        <# 
        Github SHA1 commit id
        #>
        [Parameter(Mandatory)]
        [string]
        $CommitId,

        <# 
        Absolute path of current working directory
        #>
        [Parameter(Mandatory)]
        [string]
        $WorkDir
    )

    if ([string]::IsNullOrEmpty($GitHubPat))
    {
        echo 'Environment variable ($GitHubPat) is required.'
        return 1;
    }
    else
    {
        $pat = $GitHubPat;
    }

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

    $outputDir = "$(Join-Path -Path $WorkDir -ChildPath 'output')\"
    New-Item -ItemType Directory -Force -Path $outputDir
    $WorkDir = "$(Join-Path -Path $WorkDir -ChildPath 'IIS')\"
    New-Item -ItemType Directory -Force -Path $WorkDir

    # Let's look at the artifacts
    $artifactUrl = $artifactRun.artifacts_url
    $artifacts = $(Invoke-WebRequest -Headers $header -Uri "$artifactUrl" | ConvertFrom-JSON)
    $artifacts = $artifacts.artifacts
    $artifactFinalists = @();
    $artifactExtension = ".zip"

    foreach ($artifact in $artifacts)
    {
        $downloadUrl = $artifact.archive_download_url
        $artifactName = $artifact.name
        $artifactPath = "$WorkDir$artifactName"

        echo "Downloading $artifactName"
        Retry-Command -ScriptBlock {
            Invoke-WebRequest -Headers $header -Uri "$downloadUrl" -OutFile "$artifactPath$artifactExtension"
            Expand-Archive -Path "$artifactPath$artifactExtension" -DestinationPath $artifactPath -Force
        }

        # Each artifact is a directory, let's get only packages in them.
        $packages = $(Get-ChildItem -Path $artifactPath/* -Recurse `
            -Include "*.deb", "*.rpm" `
            -Exclude "*.src*", "*dev*", "*dbg*", "*debug*" `
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

        echo ""
    }

    echo ""

    # Stage uploading files
    foreach ($artifact in $artifactFinalists)
    {
        echo "Moving : $($artifact.FullName)"
        echo "To : $(Join-Path -Path $outputDir -ChildPath $artifact.Name)"
        Move-Item -Path $artifact.FullName -Destination $(Join-Path -Path $outputDir -ChildPath $artifact.Name) -Force
    }
}


<#
    .SYNOPSIS
        This function upload the artifacts from a given "workDir" to a new GitHub release page.
    .DESCRIPTION
        1) Derived a new version string from the latest release in 'Azure/iotedge' repository
        2) Extract a relavant CHANGELOG.md section for the release page description
        3) Create a new GitHub release page against 'Azure/azure-iotedge' repository
        4) Upload an artifact from the directory with a corresponding MIMETYPE.
    .INPUTS 
        $GitHubPat ____________ (String) Environment variable contains BASE64 Github Personal Access Token (with Repos:Read&Write privilege) in UTF8 encoding
    .PARAMETER WorkDir
        Absolute path of the working directory
    .PARAMETER BranchName
        Branch name of 'Azure/iotedge' repository.
#>
function Upload-Artifacts-To-GitHub
{
    [CmdletBinding()]
    param (
        <# 
        Absolute path of current working directory
        #>
        [Parameter(Mandatory)]
        [string]
        $WorkDir,

        <# 
        Branch name of "iotedge" repository.
        This parameter is used for the script to figure out the releasing version.
        i.e. "master", "release/1.1", "release/1.2", etc.
        #>
        [Parameter(Mandatory)]
        [string]
        $BranchName
    )

    if ([string]::IsNullOrEmpty($GitHubPat))
    {
        echo 'Environment variable ($GitHubPat) is required.'
        return 1;
    }
    else
    {
        $pat = $GitHubPat;
    }

    # Get the latest release from a given branch
    echo "Fetch the latest release: "
    $url = "https://api.github.com/repos/Azure/iotedge/releases"
    # Remark: GitHub PAT in KeyVault is already base64. No need to encode it
    $header = @{
        "Accept" = "application/vnd.github.v3+json"
        "Authorization" = "token $pat"
    }
    $releaseList = $(Invoke-WebRequest -Headers $header -Uri "$url" -Method GET | ConvertFrom-JSON) `
        | where {($_.target_commitish -eq $BranchName)}
    $latestRelease = $releaseList[0]
    echo "    $($latestRelease.tag_name)"
    echo ""

    # Generate a new version tag
    $versionParts = $latestRelease.tag_name.split('.')
    ([int]$versionParts[2])++ 
    $version = $($versionParts -join('.'))
    echo "Target release: $version"
    echo ""

    # Get content of changelog to be used a release message
    echo "Fetch Changelog"
    $url = "https://api.github.com/repos/Azure/iotedge/contents/"
    $body = @{
        path = "iotedge/"
        ref = "$BranchName"
    }
    $changeLog = $((Invoke-WebRequest -Headers $header -Uri "$url" -Method GET -Body $body).Content | ConvertFrom-JSON) `
        | where {($_.name -eq "CHANGELOG.md")}
    $changeLogUrl = $changeLog.download_url

    $utf8Header = @{
        "Accept" = "application/vnd.github.v3+json"
        "Authorization" = "token $pat"
        "Content-Type"="application/json; charset=utf-8"
    }
    $pattern = "(\#\s$version\s\(\d{4}-\d{2}-\d{2}\)\s[\S\s]*?)\s?(?=\#\s$($latestRelease.tag_name)\s\(\d{4}-\d{2}-\d{2}\)\s)"
    $changeLogContent = $(Invoke-WebRequest -Headers $utf8Header -Uri "$changeLogUrl" -Method GET).Content
    $changeLogContent = [regex]::match($changeLogContent, $pattern).Groups[1].Value

    # Create a new release page
    # Ref: https://docs.github.com/en/rest/reference/repos#create-a-release
    $url = "https://api.github.com/repos/Azure/azure-iotedge/releases"
    $body = @{
        tag_name = "$version"
        target_commitish = "master"
        name = "$version"
        body = "$changeLogContent"
    }
    $release = $(Invoke-WebRequest -Headers $header -Uri "$url" -Method POST -Body "$($body | ConvertTo-Json)" | ConvertFrom-JSON)

    $artifacts = $(Get-ChildItem -Path $WorkDir)
    foreach ($artifact in $artifacts)
    {
        $artifactName = $artifact.Name

        # Extract the MIMEType from the file extension
        # Ref:
        #   Content-Type ______________ https://www.iana.org/assignments/media-types/media-types.xhtml
        #                |_____________ https://stackoverflow.com/questions/20508788/do-i-need-content-type-application-octet-stream-for-file-download
        $artifactMimeType = "application/octet-stream"
        Switch ($artifact.Extension)
        {
            ".deb"  {$artifactMimeType = "application/vnd.debian.binary-package"}
            ".rpm"  {$artifactMimeType = "application/x-rpm"}
            default {$artifactMimeType = "application/octet-stream"}
        }

        # Construct the request header & body
        # Ref:
        #   GitHub API ________________ https://docs.github.com/en/rest/reference/repos#upload-a-release-asset
        $multiUploadHeader = @{
            "Accept" = "application/vnd.github.v3+json"
            "Authorization" = "token $pat"
            "Name" = "$artifactName"
            "Content-Type" = "$artifactMimeType"
        }
        $body = [System.IO.File]::ReadAllBytes($($artifact.FullName))

        # Upload the artifacts from the $workDir
        echo "Uploading : $artifactName"
        $uploadUrl = [regex]::match($release.upload_url, "^(.+?)assets").Groups[0].Value
        $uploadUrl += "?name=$artifactName"
        Invoke-WebRequest -Headers $multiUploadHeader -Uri "$uploadUrl" -Method POST -Body $body
    }
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