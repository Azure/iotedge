if (-not (Test-Path env:FUNCTION_ROOT_FILEADDRESS)) { throw "Environment variable FUNCTION_ROOT_FILEADDRESS not set." }
if (-not (Test-Path env:FUNCTION_FILE_NAME)) { throw "Environment variable FUNCTION_FILE_NAME not set." }

$baseFolder = (Get-Item -Path ".\" -Verbose).FullName

Write-Host "Downloading function."
New-Item -ItemType Directory -Force -Path $baseFolder
$packageZip =  Join-Path -Path $baseFolder -ChildPath $env:FUNCTION_FILE_NAME
$packageZip += ".zip"
$url = $env:FUNCTION_ROOT_FILEADDRESS

Write-Host "Download $url to $packageZip"
Invoke-WebRequest -Uri $url -OutFile $packageZip

Write-Host "Extracting function package $packageZip to $baseFolder folder."
Expand-Archive -Path $packageZip -DestinationPath $baseFolder

Write-Host "Cleaning up."
Remove-Item $packageZip

Write-Host "Start Webjobs script host."
dotnet WebJobs.Script.Host\WebJobs.Script.Host.dll $env:FUNCTION_FILE_NAME