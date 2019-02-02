# TODO replace with MS released docker
if (-not (Test-Path -Path dockerd.exe))
{
    Invoke-WebRequest https://master.dockerproject.org/windows/x86_64/dockerd.exe -out dockerd.exe
}
if (-not (Test-Path -Path docker.exe))
{
    Invoke-WebRequest https://master.dockerproject.org/windows/x86_64/docker.exe -out docker.exe
}

# VERSION is either 1.0.7~dev or 1.0.7
$splitVersion = $env:VERSION -split "~"
if ($splitVersion.Length -eq 1) {
    $version = ("{0}.0" -f $env:VERSION)
}
else {
    # we need 255^2 tops per segment
    $first = ($splitVersion -split "\.")[0]
    $splitSecond = ($splitVersion -split "\.")
    $dateSegment = ($splitSecond[-2])[-8..-1] -join ""
    $date = [datetime]::ParseExact($dateSegment, "yyyyMMdd", $null)
    $third = ("{0}{1}" -f ($date.ToString("yy")), $date.DayOfYear.ToString("000"))
    $version = "0.{0}.{1}.{2}" -f $first, $third, $splitSecond[-1]
}

Write-Host "Using version $version"

Function New-Package([string]$Name)
{
    $pkggen = "${Env:ProgramFiles(x86)}\Windows Kits\10\tools\bin\i386\pkggen.exe"
    $manifest = "edgelet\build\windows\$Name.wm.xml"
    $cwd = "."
    Invoke-Expression "& '$pkggen' $manifest /universalbsp /variables:'_REPO_ROOT=..\..\..'  /cpu:amd64 /version:$version"
}

New-Package -Name "iotedge"
New-Package -Name "iotedge-moby-cli"
New-Package -Name "iotedge-moby-engine"