# Run this on E2E test agent in PWSH
function Setup-BaseImage-Script
{
    # Change current directory to ../iotedge
    $rootDir = $env:BUILD_SOURCESDIRECTORY
    if (-not $rootDir) {
        $rootDir = [IO.Path]::Combine($PSScriptRoot, "..", "..")
    }

    cd $rootDir

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

    Get-ChildItem -Recurse -Filter "Dockerfile" | `
        Select-String "ARG base_tag=" | `
        ForEach-Object { $_.Line -replace 'ARG base_tag=', '' } | `
        Sort-Object -Unique
}


function Get-Dockerfile-Locations
{
    Setup-BaseImage-Script | Out-Null

    # Get all the Dockerfile file location
    $fileLocale = $(Get-ChildItem -Recurse -Filter "Dockerfile" | Where {$_.FullName -notlike "*generic-mqtt-tester*"})
    return $fileLocale;
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
        $NewASPNetCoreVersion,

        <# 
        Object array contain file paths to base image dockerfile
        #>
        [Parameter(Mandatory=$false)]
        [Object[]]
        $FileLocale
    )

    if ($FileLocale.Count -gt 0)
    {
        $fileLocale = $FileLocale
    }
    else
    {
        $fileLocale = Get-Dockerfile-Locations
    }

    # Replace the underlying ASP .Net Core to the new version
    # Assuming the ARM64 & ARM32 both use the same *-bionic-arm* ASP .Net Core image tag
    $baseAspNetLocale = $($($fileLocale | Convert-Path) -like "*\base\*" -notlike "*\bin\*" | Resolve-path)
    foreach ($file in $baseAspNetLocale)
    {
        (Get-Content -Encoding utf8 $file.Path) |
        Foreach-Object { $_ -replace "ARG base_tag=.*.-bionic-arm", "ARG base_tag=$NewASPNetCoreVersion-bionic-arm" } |
        Set-Content -Encoding utf8 $file.Path 
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


function Update-AMD64-BaseImages
{
    [CmdletBinding()]
    param (
        <# 
        The new version of ASP .NET Core. 
        This version is applied to both 'alpine' and 'nanoserver'
        Ex: The new ASP .NET Core tag is 3.1.15-alpine3.13, the $NewASPNetCoreVersion = 3.1.15
        #>
        [Parameter(Mandatory)]
        [string]
        $NewASPNetCoreVersion,

        <# 
        The new version of Alpine base image. 
        This version is only applied to 'alpine' images
        Ex: The new ASP .NET Core tag is 3.1.15-alpine3.13, the $NewAlpineVersion = 3.13
        #>
        [Parameter(Mandatory)]
        [string]
        $NewAlpineVersion,

        <# 
        Object array contain file paths to base image dockerfile
        #>
        [Parameter(Mandatory=$false)]
        [Object[]]
        $FileLocale
    )

    <# REMARK: This function does not update 'debian' and 'azure function' base images!!! #>

    if ($FileLocale.Count -gt 0)
    {
        $fileLocale = $FileLocale
    }
    else
    {
        $fileLocale = Get-Dockerfile-Locations
    }

    # Replace the underlying ASP .Net Core to the new version for 'nanoserver'
    $baseAspNetLocale = $($($fileLocale | Convert-Path) -like "*\windows\amd64\*" -notlike "*\bin\*" | Resolve-path)
    foreach ($file in $baseAspNetLocale)
    {
        # Note: The following dockerfile(s) are not automatically updated by this script
        #    \iotedge\edge-modules\functions\samples\docker\windows\amd64\Dockerfile_______(Azure Function)
        #    \iotedge\tools\snitch\snitcher\docker\windows\amd64\Dockerfile _______________(debian10)
        (Get-Content -Encoding utf8 $file.Path) |
        Foreach-Object { $_ -replace "ARG base_tag=.*.-nanoserver-1809", "ARG base_tag=$NewASPNetCoreVersion-nanoserver-1809" } |
        Set-Content -Encoding utf8 $file.Path 
    }

    # Replace the underlying ASP .Net Core to the new version for 'alpine'
    $baseAspNetLocale = $($($fileLocale | Convert-Path) -like "*\linux\amd64\*" -notlike "*\bin\*" | Resolve-path)
    foreach ($file in $baseAspNetLocale)
    {
        # Note: The following dockerfile(s) are not automatically updated by this script
        #    \iotedge\edge-modules\functions\samples\docker\linux\amd64\Dockerfile ________(Azure Function)
        #    \iotedge\tools\snitch\prep-mail\docker\linux\amd64\Dockerfile ________________(Deprecating)
        #    \iotedge\tools\snitch\snitcher\docker\linux\amd64\Dockerfile _________________(Deprecating)
        #    \iotedge\edgelet\iotedged\docker\linux\amd64\Dockerfile ______________________(debian10)
        (Get-Content -Encoding utf8 $file.Path) |
        Foreach-Object { $_ -replace "ARG base_tag=.*.-alpine[\d]+\.[\d]+", "ARG base_tag=$NewASPNetCoreVersion-alpine$NewAlpineVersion" } |
        Foreach-Object { $_ -replace "FROM alpine:[\d]+\.[\d]+", "FROM alpine:$NewAlpineVersion" } |
        Set-Content -Encoding utf8 $file.Path
    }
}


function Update-BaseImages
{
    [CmdletBinding()]
    param (
        <#
        The new version of ASP .NET Core
        Ex: The new ASP .NET Core tag is 2.1.23-bionic-arm32, the $NewASPNetCoreVersion = 2.1.23
        #>
        [Parameter(Mandatory)]
        [string]
        $NewASPNetCoreVersion,

        <#
        The new version of Alpine base image.
        This version is only applied to 'alpine' images
        Ex: The new ASP .NET Core tag is 3.1.15-alpine3.13, the $NewAlpineVersion = 3.13
        #>
        [Parameter(Mandatory)]
        [string]
        $NewAlpineVersion
    )

    echo "Lookuping location of dockerfiles..."
    $fileLocale = Get-Dockerfile-Locations

    echo "Updating base image for ARM32 & ARM64..."
    Update-ARM-BaseImages -NewASPNetCoreVersion "$NewASPNetCoreVersion" -FileLocale $fileLocale -ErrorAction Stop

    echo "Updating base image for AMD64..."
    Update-AMD64-BaseImages -NewASPNetCoreVersion "$NewASPNetCoreVersion" -NewAlpineVersion "$NewAlpineVersion" -FileLocale $fileLocale -ErrorAction Stop
}