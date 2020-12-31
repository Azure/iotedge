# Run this on E2E test agent in PWSH
function Setup-BaseImage-Script
{
    # Change current directory to ../iotedge
    try
    {
        $(cd $(Build.SourcesDirectory)) 
    }
    catch
    {
        $curDir = pwd
        $iotedgeDir=$($curDir -split("iotedge", 2))
        $iotedgeDir = $iotedgeDir[0] + "iotedge"
        cd $iotedgeDir
    }

    $isDotNetInstalled = ((gp HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*).DisplayName -Match "Microsoft .NET Core Runtime").Length -gt 0;
    if ($isDotNetInstalled)
    {
        # Cleaning build artifacts as it slow down the search
        dotnet clean 
        dotnet clean -c release
    }
}


function Get-Unique-BaseImages
{
    Setup-BaseImage-Script

    # Get all the Dockerfile file with string "ARG base_tag="
    $fileLocal = $(Get-ChildItem -Recurse -Filter "Dockerfile" | Select-String "ARG base_tag=")

    # Remove the filenames
    $i = 1; $baseImageNames = $($fileLocal -split "ARG base_tag=" | Where-Object { $i % 2 -eq 0; $i++ })

    # Show unique base image we are using
    $baseImageNames | Sort-Object -Unique
}


function Update-ARM-BaseImages
{
    [CmdletBinding()]
    param (
        <# 
        The new version of ASP .NET Core
        Ex: The new ASP .NET Core tag is 2.1.23-bionic-arm32, the $NewASPNetCoreVersion = 2.1.23 
        #>
        [Parameter(Mandatory)]
        [string]
        $NewASPNetCoreVersion
    )

    Setup-BaseImage-Script

    # Get all the Dockerfile file location
    $fileLocale = $(Get-ChildItem -Recurse -Filter "Dockerfile" )

    # Replace the underlying ASP .Net Core to the new version
    # Assuming the ARM64 & ARM32 both use the same *-bionic-arm* ASP .Net Core image tag
    $baseAspNetLocale = $($($fileLocale | Convert-Path) -like "*\base\*" | Resolve-path)
    foreach ($file in $baseAspNetLocale)
    {
        (Get-Content $file.Path) |
        Foreach-Object { $_ -replace "ARG base_tag=.*.-bionic-arm", "ARG base_tag=$NewASPNetCoreVersion-bionic-arm" } |
        Set-Content $file.Path
    }

    # Update the places where the base images are used
    $baseImageLocale = $($fileLocale | Select-String "ARG base_tag=.*.-linux-arm" | Select-Object -Unique Path)
    foreach ($file in $($baseImageLocale | Resolve-path))
    {
        # Increment the last digit by 1
        $fileContent = (Get-Content $file.Path);
        $curVersion = $($fileContent -like "ARG base_tag=*" ).split("=")[1].split("-")[0];
        $splitVersion = $curVersion.split(".", 4);
        $incrementedSegment = $([int]$splitVersion[3]) + 1;
        $newBaseImageVersion = $($splitVersion[0,1,2] + $incrementedSegment -join ".");

        # Replace the version
        $fileContent |
        Foreach-Object { $_ -replace "ARG base_tag=.*.-linux-arm", "ARG base_tag=$newBaseImageVersion-linux-arm" } |
        Set-Content $file.Path
    }
}