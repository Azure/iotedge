Param(
    [Switch] $CreateTemplate, 
    [Switch] $CreateCab, 
    [Switch] $SkipInstallCerts, 
    [Switch] $Arm
)

$EdgeCab = "Microsoft-Azure-IoTEdge.cab"
$EdgeTemplate = "Package-Template"

# Bring in util functions
$util = Join-Path -Path $PSScriptRoot -ChildPath "util.ps1"
. $util

Function New-Cabinet([String] $Destination, [String[]] $Files, [String] $Path)
{
    $Ddf = [IO.Path]::GetTempFileName()
    $CabinetName = Split-Path -Leaf $Destination
    $DiskDirectory = Split-Path -Parent $Destination
    if (-not $DiskDirectory) {
        $DiskDirectory = "."
    }
    $DiskDirectory = (Get-Item -Path $DiskDirectory).FullName
    $Directories = $Files | Group-Object -Property { Split-Path -Parent $_ }
    $DdfContent = @"
.Option Explicit
.Set SourceDir=$Path
.Set DiskDirectoryTemplate=$DiskDirectory
.Set CabinetNameTemplate=$CabinetName
.Set CompressionType=LZX
.Set Compress=on
.Set UniqueFiles=Off
.Set Cabinet=On
.Set MaxDiskSize=0

"@
    $Directories | ForEach-Object {
        $Directory = $_.Name
        if (-not $Directory)
        {
            $Directory = " ;"
        }
        $Files = $_.Group
        $DdfContent += @"
.Set DestinationDir=$Directory
$($OFS="`r`n"; $Files)

"@
    }
    $DdfContent | Out-File $Ddf -Encoding Ascii

    $DdfContent

    makecab.exe /f $Ddf
    if ($LASTEXITCODE) {
        Throw "Failed to create cab"
    }

    Remove-Item $Ddf
}

Function New-Package([string] $Name, [string] $Version)
{
    $pkggen = "${Env:ProgramFiles(x86)}\Windows Kits\10\tools\bin\i386\pkggen.exe"
    $manifest = "edgelet\build\windows\$Name.wm.xml"
    $mobyroot = if($Arm) {"C:\dependencies\win-arm"} else { "..\..\.." }
    $cwd = "."
    $arch = if($Arm) { 'thumbv7a-pc-windows-msvc'} else { '' }
    $cpu = if($Arm) {'arm'} else { 'amd64' }

    if($Arm)
    {
        # pkggen cannot find makecat.exe from below folder at runtime
        $env:PATH = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x64;" + $env:PATH
    }

    Invoke-Expression "& '$pkggen' $manifest /universalbsp /variables:'_REPO_ROOT=..\..\..;_OPENSSL_ROOT_DIR=$env:OPENSSL_ROOT_DIR;_Arch=$arch' /cpu:$cpu /version:$Version"
    if ($LASTEXITCODE) {
        Throw "Failed to package cab"
    }

    if (Test-Path $EdgeTemplate) {
        Remove-Item -Path $EdgeTemplate -Recurse -Force
    }
    New-Item -ItemType Directory -Path $EdgeTemplate
    Invoke-Expression "& ${Env:SystemRoot}\system32\Expand.exe $EdgeCab -f:* $EdgeTemplate"
    if ($LASTEXITCODE) {
        Throw "Failed to expand cab"
    }
    Remove-Item -Path $EdgeCab
}

if ($CreateTemplate) {
    $docker_cli_uri = if($Arm)
    {
        "https://iottools.blob.core.windows.net/iotedge-armtools/moby-cli.zip"
    }
    else
    {
        "https://github.com/Azure/azure-iotedge/releases/download/1.0.5/moby-cli_3.0.2.zip"
    }
    $docker_engine_uri = if($Arm)
    {
        "https://iottools.blob.core.windows.net/iotedge-armtools/moby-engine.zip"
    }
    else
    {
        "https://conteng.blob.core.windows.net/mby/moby-engine_3.0.3.zip"
    }

    $env:PATH = "$env:PATH;C:\Program Files (x86)\Windows Kits\10\bin\x64;C:\Program Files (x86)\Windows Kits\10\tools\bin\i386"
    $env:SIGNTOOL_OEM_SIGN = '/a /s my /i "Windows OEM Intermediate 2017 (TEST ONLY)" /n "Windows OEM Test Cert 2017 (TEST ONLY)" /fd SHA256'
    $env:SIGN_MODE = 'Test'
    $env:SIGN_OEM = '1'
    $env:SIGN_WITH_TIMESTAMP = '0'
    $env:WSKCONTENTROOT = 'C:\Program Files (x86)\Windows Kits\10'

    if (-not $SkipInstallCerts) {
        cmd /c installoemcerts.cmd
    }

    $ProgressPreference = 'SilentlyContinue'

    Write-Host "Download $docker_cli_uri"
    Invoke-WebRequest $docker_cli_uri -out "moby-cli.zip" -UseBasicParsing
    if (Test-Path "moby-cli") {
        Remove-Item -Path "moby-cli" -Recurse -Force
    }
    
    Write-Host "Extract $docker_cli_uri"
    Expand-Archive -Path "moby-cli.zip" -DestinationPath "moby-cli"
    
    Write-Host "Download $docker_engine_uri"
    Invoke-WebRequest $docker_engine_uri -out "moby-engine.zip" -UseBasicParsing
    if (Test-Path "moby-engine") {
        Remove-Item -Path "moby-engine" -Recurse -Force
    }
    
    Write-Host "Extract $docker_engine_uri"
    Expand-Archive -Path "moby-engine.zip" -DestinationPath "moby-engine"

    #
    # IoTEdge
    #

    Write-Host ("IoTEdge source version '{0}'" -f $env:VERSION)

    # VERSION is either 1.0.7~dev or 1.0.7
    $splitVersion = $env:VERSION -split "~"
    if (($splitVersion.Length -eq 1) -or ($splitVersion[1] -notmatch "^\w+\d{8}\.\d+$")) {
        if ($splitVersion.Length -eq 1) {
            $version = $env:VERSION
        }
        else {
            $version = $splitVersion[0]
        }
        $splitVersion = $version -split "\."
        if ($splitVersion.Length -eq 3) {
            $version = ("{0}.0" -f $version)
        }
        if ($version -notmatch "\d+\.\d+\.\d+\.\d+") {
            throw "Unexpected version string; Windows package requires VERSION in form major.minor.build.revision, each segment having 0-65535."
        }
    }
    elseif ($splitVersion[1] -match "^\w+\d{8}\.\d+$") { # internal build with version set to something like: '1.0.8~dev20190328.3'
        # we need 255^2 tops per segment
        $major = ($splitVersion[0] -split "\.")[0]
        $splitSuffix = ($splitVersion[1] -split "\.")
        $dateSegment = ($splitSuffix[-2])[-8..-1] -join ""
        $date = [datetime]::ParseExact($dateSegment, "yyyyMMdd", $null)
        $dateEncoded = ("{0}{1}" -f ($date.ToString("yy")), $date.DayOfYear.ToString("000"))
        $buildPerDay = $splitSuffix[-1]
        $version = "0.{0}.{1}.{2}" -f $major, $dateEncoded, $buildPerDay
    }
    else {
        throw "Unexpected version string."
    }

    Write-Host "IoTEdge using version '$version'"

    New-Package -Name "iotedge" -Version $version
}
elseif ($CreateCab) {
    $TemplateDirLength = ((Get-Item -Path $EdgeTemplate).FullName.Length + 1)
    $Files = Get-ChildItem -Path $EdgeTemplate -Recurse | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
        return $_.FullName.Remove(0, $TemplateDirLength)
    }
    New-Cabinet -Destination $EdgeCab -Files $Files -Path $EdgeTemplate
}
