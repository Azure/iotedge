$docker_cli_uri = "https://github.com/Azure/azure-iotedge/releases/download/1.0.5/moby-cli_3.0.2.zip"
$docker_engine_uri = "https://conteng.blob.core.windows.net/mby/moby-engine_3.0.3.zip"

$env:PATH = "$env:PATH;C:\Program Files (x86)\Windows Kits\10\bin\x64;C:\Program Files (x86)\Windows Kits\10\tools\bin\i386"
$env:SIGNTOOL_OEM_SIGN = '/a /s my /i "Windows OEM Intermediate 2017 (TEST ONLY)" /n "Windows OEM Test Cert 2017 (TEST ONLY)" /fd SHA256'
$env:SIGN_MODE = 'Test'
$env:SIGN_OEM = '1'
$env:SIGN_WITH_TIMESTAMP = '0'
$env:WSKCONTENTROOT = 'C:\Program Files (x86)\Windows Kits\10'

cmd /c installoemcerts.cmd

Invoke-WebRequest $docker_cli_uri -out "moby-cli.zip"
Expand-Archive -Path "moby-cli.zip" -DestinationPath "moby-cli"

Invoke-WebRequest $docker_engine_uri -out "moby-engine.zip"
Expand-Archive -Path "moby-engine.zip" -DestinationPath "moby-engine"

Function New-Package([string] $Name, [string] $Version)
{
    $pkggen = "${Env:ProgramFiles(x86)}\Windows Kits\10\tools\bin\i386\pkggen.exe"
    $manifest = "edgelet\build\windows\$Name.wm.xml"
    $cwd = "."
    Invoke-Expression "& '$pkggen' $manifest /universalbsp /variables:'_REPO_ROOT=..\..\..;_OPENSSL_ROOT_DIR=$env:OPENSSL_ROOT_DIR' /cpu:amd64 /version:$Version"
}

#
# IoTEdge
#

Write-Host ("IoTEdge source version '{0}'" -f $env:VERSION)

# VERSION is either 1.0.7~dev or 1.0.7
$splitVersion = $env:VERSION -split "~"
if ($splitVersion.Length -eq 1) {
    $version = $env:VERSION
    $splitVersion = $version -split "\."
    if ($splitVersion.Length -eq 3) {
        $version = ("{0}.0" -f $version)
    }
    if ($version -notmatch "\d+\.\d+\.\d+\.\d+") {
        throw "Windows package requires VERSION in form major.minor.build.revision, each segment having 0-65535"
    }
}
else {
    # we need 255^2 tops per segment
    $major = ($splitVersion[0] -split "\.")[0]
    $splitSuffix = ($splitVersion[1] -split "\.")
    $dateSegment = ($splitSuffix[-2])[-8..-1] -join ""
    $date = [datetime]::ParseExact($dateSegment, "yyyyMMdd", $null)
    $dateEncoded = ("{0}{1}" -f ($date.ToString("yy")), $date.DayOfYear.ToString("000"))
    $buildPerDay = $splitSuffix[-1]
    $version = "0.{0}.{1}.{2}" -f $major, $dateEncoded, $buildPerDay
}

Write-Host "IoTEdge using version '$version'"

New-Package -Name "iotedge" -Version $version
