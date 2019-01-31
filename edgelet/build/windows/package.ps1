# TODO replace with MS released docker
if (-not (Test-Path -Path dockerd.exe))
{
    Invoke-WebRequest https://master.dockerproject.org/windows/x86_64/dockerd.exe -out dockerd.exe
}
if (-not (Test-Path -Path docker.exe))
{
    Invoke-WebRequest https://master.dockerproject.org/windows/x86_64/docker.exe -out docker.exe
}

$pkggen = "c:\Program Files (x86)\Windows Kits\10\tools\bin\i386\pkggen.exe"
$manifest = "edgelet\build\windows\package.wm.xml"
$version = "{0}.0" -f $env:VERSION
$cwd = "."
Invoke-Expression "& '$pkggen' $manifest /universalbsp /variables:'_REPO_ROOT=..\..\..'  /cpu:amd64 /version:$version"