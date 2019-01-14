<#
 # Builds and publishes to target/publish/ all .NET Core solutions in the repo
 #>

param (
    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $AgentWorkFolder = $Env:AGENT_WORKFOLDER,

    [ValidateNotNullOrEmpty()]
    [ValidateScript( {Test-Path $_ -PathType Container})]
    [String] $BuildRepositoryLocalPath = $Env:BUILD_REPOSITORY_LOCALPATH,
    
    [ValidateNotNullOrEmpty()]
    [String] $BuildBinariesDirectory = $Env:BUILD_BINARIESDIRECTORY,

    [ValidateSet("Debug", "CheckInBuild", "Release")]
    [String] $Configuration = $Env:CONFIGURATION,

    [ValidateNotNull()]
    [String] $BuildId = $Env:BUILD_BUILDID,

    [ValidateNotNull()]
    [String] $BuildSourceVersion = $Env:BUILD_SOURCEVERSION,
    
    [Switch] $UpdateVersion,
    [Switch] $PublishTests
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

<#
 # Prepare environment
 #>

Import-Module ([IO.Path]::Combine($PSScriptRoot, "..", "Defaults.psm1")) -Force

if (-not $AgentWorkFolder) {
    $AgentWorkFolder = DefaultAgentWorkFolder
}

if (-not $BuildRepositoryLocalPath) {
    $BuildRepositoryLocalPath = DefaultBuildRepositoryLocalPath
}
 
if (-not $BuildBinariesDirectory) {
    $BuildBinariesDirectory = DefaultBuildBinariesDirectory $BuildRepositoryLocalPath
}

if (-not $Configuration) {
    $Configuration = DefaultConfiguration
}

if (-not $BuildId) {
    $BuildId = DefaultBuildId
}

if (-not $BuildSourceVersion) {
    $BuildSourceVersion = DefaultBuildSourceVersion
}

$SLN_PATTERN = "Microsoft.Azure.*.sln"
$TEST_CSPROJ_PATTERN = "Microsoft.Azure*Test.csproj"

$DOTNET_PATH = [IO.Path]::Combine($AgentWorkFolder, "dotnet", "dotnet.exe")
$PUBLISH_FOLDER = Join-Path $BuildBinariesDirectory "publish"
$RELEASE_TESTS_FOLDER = Join-Path $BuildBinariesDirectory "release-tests"
$VERSIONINFO_FILE_PATH = Join-Path $BuildRepositoryLocalPath "versionInfo.json"

$SRC_SCRIPTS_DIR = Join-Path $BuildRepositoryLocalPath "scripts"
$PUB_SCRIPTS_DIR = Join-Path $PUBLISH_FOLDER "scripts"
$PUB_STRESS_DIR = Join-Path $PUBLISH_FOLDER "stress"
$SRC_BIN_DIR = Join-Path $BuildRepositoryLocalPath "bin"
$PUB_BIN_DIR = Join-Path $PUBLISH_FOLDER "bin"
$SRC_E2E_TEMPLATES_DIR = Join-Path $BuildRepositoryLocalPath "e2e_deployment_files"
$PUB_E2E_TEMPLATES_DIR = Join-Path $PUBLISH_FOLDER "e2e_deployment_files"
$TEST_SCRIPTS_DIR = Join-Path $RELEASE_TESTS_FOLDER "scripts"

if (-not (Test-Path $DOTNET_PATH -PathType Leaf)) {
    throw "$DOTNET_PATH not found"
}

if (Test-Path $BuildBinariesDirectory -PathType Container) {
    Remove-Item $BuildBinariesDirectory -Force -Recurse
}

<#
 # Update version
 #>

if ($UpdateVersion) {
    if (Test-Path $VERSIONINFO_FILE_PATH -PathType Leaf) {
        Write-Host "`nUpdating versionInfo.json with the build ID and commit ID.`n"
        ((Get-Content $VERSIONINFO_FILE_PATH) `
                -replace "BUILDNUMBER", $BuildId) `
            -replace "COMMITID", $BuildSourceVersion |
            Out-File $VERSIONINFO_FILE_PATH
    }
    else {
        Write-Host "`nversionInfo.json not found.`n"
    }
}
else {
    Write-Host "`nSkipping versionInfo.json update.`n"
}

<#
 # Build solutions
 #>

Write-Host "`nBuilding all solutions in repo`n"

foreach ($Solution in (Get-ChildItem $BuildRepositoryLocalPath -Include $SLN_PATTERN -Recurse)) {
    Write-Host "Building Solution - $Solution"
    &$DOTNET_PATH build -c $Configuration -o $BuildBinariesDirectory $Solution |
        Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed building $Solution."
    }
}

<#
 # Publish applications
 #>

Write-Host "`nPublishing .NET Core apps`n"

$appProjectList = New-Object 'System.Collections.Generic.List[String]'
$appProjectList.Add("Microsoft.Azure.Devices.Edge.Agent.Service.csproj")
$appProjectList.Add("Microsoft.Azure.Devices.Edge.Hub.Service.csproj")
$appProjectList.Add("SimulatedTemperatureSensor.csproj")
$appProjectList.Add("TemperatureFilter.csproj")
$appProjectList.Add("load-gen.csproj")
$appProjectList.Add("MessagesAnalyzer.csproj")
$appProjectList.Add("DirectMethodSender.csproj")
$appProjectList.Add("DirectMethodReceiver.csproj")

# Download latest rocksdb ARM32 library
$rocksdbARMUri = "https://edgebuild.blob.core.windows.net/rocksdb/rocksdb-arm.dll"
$tempPath = [System.IO.Path]::GetTempPath()
$rocksdbARMSourcePath = Join-Path $tempPath "rocksdb.dll"
Invoke-WebRequest -Uri $rocksdbARMUri -OutFile $rocksdbARMSourcePath

foreach ($appProjectFileName in $appProjectList) {
    $appProjectFilePath = Get-ChildItem -Include *.csproj -File -Recurse |Where-Object {$_.Name -eq "$appProjectFileName"}|Select-Object -first 1|Select -ExpandProperty "FullName"

    if (-Not $appProjectFilePath) {
        throw "Can't find app project with name $appProjectFileName"
    }

    Write-Host "Publishing App Project - $appProjectFilePath"
    $ProjectPublishPath = Join-Path $PUBLISH_FOLDER ($appProjectFileName -replace @(".csproj", ""))
    &$DOTNET_PATH publish -f netcoreapp2.1 -c $Configuration -o $ProjectPublishPath $appProjectFilePath |
        Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed app publishing $appProjectFilePath."
    }
    
    # Copy rocksdb ARM32 version to native/arm folder; this rocksdb.dll statically linked with snappy dll.
    if ($appProjectFileName -eq "Microsoft.Azure.Devices.Edge.Agent.Service.csproj" -or $appProjectFileName -eq "Microsoft.Azure.Devices.Edge.Hub.Service.csproj")
    {
        $nativeARMFolder = Join-Path $ProjectPublishPath "native\arm"
        New-Item -ItemType Directory -Force -Path $nativeARMFolder
        $rocksdbARMDestPath = Join-Path $nativeARMFolder "rocksdb.dll"
        Copy-Item $rocksdbARMSourcePath $rocksdbARMDestPath -Force
    }
}

<#
 # Publish libraries
 #>

Write-Host "`nPublishing .NET Core libs`n"

$libProjectList = New-Object 'System.Collections.Generic.List[String]'
$libProjectList.Add("Microsoft.Azure.WebJobs.Extensions.EdgeHub.csproj")
$libProjectList.Add("EdgeHubTriggerCSharp.csproj")

foreach ($libProjectFileName in $libProjectList) {
    $libProjectFilePath = Get-ChildItem -Include *.csproj -File -Recurse |Where-Object {$_.Name -eq "$libProjectFileName"}|Select-Object -first 1|Select -ExpandProperty "FullName"

    if (-Not $libProjectFilePath) {
        throw "Can't find lib project with name $libProjectFilePath"
    }

    Write-Host "Publishing Lib Project - $libProjectFilePath"
    $ProjectPublishPath = Join-Path $PUBLISH_FOLDER ($libProjectFileName -replace @(".csproj", ""))
    &$DOTNET_PATH publish -f netstandard2.0 -c $Configuration -o $ProjectPublishPath $libProjectFilePath |
        Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed lib publishing $libProjectFilePath."
    }
}

<#
 # Copy remaining files
 #>

Write-Host "Copying $SRC_SCRIPTS_DIR to $PUB_SCRIPTS_DIR"
Copy-Item $SRC_SCRIPTS_DIR $PUB_SCRIPTS_DIR -Recurse -Force 

Write-Host "Copying $SRC_BIN_DIR to $PUB_BIN_DIR"
Copy-Item $SRC_BIN_DIR $PUB_BIN_DIR -Recurse -Force 

Write-Host "Copying $SRC_E2E_TEMPLATES_DIR"
Copy-Item $SRC_E2E_TEMPLATES_DIR $PUB_E2E_TEMPLATES_DIR -Recurse -Force

<#
 # Publish tests
 #>

if ($PublishTests) {
    Write-Host "`nPublishing .NET Core Tests`n"
    foreach ($Project in (Get-ChildItem $BuildRepositoryLocalPath -Include $TEST_CSPROJ_PATTERN -Recurse)) {
        Write-Host "Publishing - $Project"
        $ProjectPublishPath = Join-Path $RELEASE_TESTS_FOLDER "target"
        &$DOTNET_PATH publish -f netcoreapp2.1 -c $Configuration -o $ProjectPublishPath $Project |
            Write-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Failed publishing $Project."
        }

        $ProjectCopyPath = Join-Path $RELEASE_TESTS_FOLDER $Project.BaseName
        Write-Host "Copying $Project to $ProjectCopyPath"
        Copy-Item $Project $ProjectCopyPath -Force
    }

    Write-Host "Copying $SRC_SCRIPTS_DIR to $TEST_SCRIPTS_DIR"
    Copy-Item $SRC_SCRIPTS_DIR $TEST_SCRIPTS_DIR -Force -Recurse
    Copy-Item (Join-Path $BuildRepositoryLocalPath "Nuget.config") $RELEASE_TESTS_FOLDER
}
else {
    Write-Host "`nSkipping publication of .NET Core Tests`n"
}

<#
 # Publish IoTEdgeQuickstart
 #>
$IoTEdgeQuickstartProjectFolder = Join-Path $BuildRepositoryLocalPath "smoke/IoTEdgeQuickstart"
$IoTEdgeQuickstartPublishBaseFolder = Join-Path $PUBLISH_FOLDER "IoTEdgeQuickstart"

Write-Host "Publishing - IoTEdgeQuickstart x64"
$ProjectPublishPath = Join-Path $IoTEdgeQuickstartPublishBaseFolder "x64"
&$DOTNET_PATH publish -f netcoreapp2.1 -r "win10-x64" -c $Configuration -o $ProjectPublishPath $IoTEdgeQuickstartProjectFolder |
	Write-Host
if ($LASTEXITCODE -ne 0) {
	throw "Failed publishing IoTEdgeQuickstart x64."
}

Write-Host "Publishing - IoTEdgeQuickstart arm32"
$ProjectPublishPath = Join-Path $IoTEdgeQuickstartPublishBaseFolder "arm32v7"
&$DOTNET_PATH publish -f netcoreapp2.1 -r "win10-arm" -c $Configuration -o $ProjectPublishPath $IoTEdgeQuickstartProjectFolder |
	Write-Host
if ($LASTEXITCODE -ne 0) {
	throw "Failed publishing IoTEdgeQuickstart arm32."
}

<#
 # Publish LeafDevice
 #>
$LeafDeviceProjectFolder = Join-Path $BuildRepositoryLocalPath "smoke/LeafDevice"
$LeafDevicePublishBaseFolder = Join-Path $PUBLISH_FOLDER "LeafDevice"

Write-Host "Publishing - LeafDevice x64"
$ProjectPublishPath = Join-Path $LeafDevicePublishBaseFolder "x64"
&$DOTNET_PATH publish -f netcoreapp2.1 -r "win10-x64" -c $Configuration -o $ProjectPublishPath $LeafDeviceProjectFolder |
	Write-Host
if ($LASTEXITCODE -ne 0) {
	throw "Failed publishing LeafDevice x64."
}

Write-Host "Publishing - LeafDevice arm32"
$ProjectPublishPath = Join-Path $LeafDevicePublishBaseFolder "arm32v7"
&$DOTNET_PATH publish -f netcoreapp2.1 -r "win10-arm" -c $Configuration -o $ProjectPublishPath $LeafDeviceProjectFolder |
	Write-Host
if ($LASTEXITCODE -ne 0) {
	throw "Failed publishing LeafDevice arm32."
}