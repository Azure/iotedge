###############################################################################
# This Script builds all Edge application in their respective docker containers
# This script expects that buildBranch.bat was invoked earlier and all the
# necessary application files and their Dockerfile be published in
# directory identified by environement variable BUILD_BINARIESDIRECTORY
###############################################################################

Param(
    # Docker registry required to build, tag and run the module
    [Parameter(Mandatory=$true)]
    [String]$Registry,

    # Docker Registry Username
    [Parameter(Mandatory=$true)]
    [String]$Username,

    # Docker Username's password
    [Parameter(Mandatory=$true)]
    [String]$Password,

    # Docker Image Version
    [ValidateNotNullOrEmpty()]
    [String]$ImageVersion = $Env:BUILD_BUILDNUMBER,

    # Target architecture
    [ValidateNotNullOrEmpty()]
    [String]$TargetArch = $Env:PROCESSOR_ARCHITECTURE,

    # Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY
    [ValidateNotNullOrEmpty()]
    [String]$BinDir = $Env:BUILD_BINARIESDIRECTORY,

    # Do not push images to the registry
    [Switch]$SkipPush
)

if (-not $ImageVersion)
{
    Throw "Docker image version '$ImageVersion' not found"
}

if (-not $BinDir -or -not (Test-Path $BinDir))
{
    Throw "Bin directory '$BinDir' does not exist or is invalid"
}

$PublishDir = Join-Path $BinDir "publish"

if (-not (Test-Path $PublishDir))
{
    Throw "Publish directory '$PublishDir' does not exist or is invalid"
}

switch ($TargetArch)
{
    "AMD64" { $TargetArch = "x64" }
    default { throw "Unsupported arch '$TargetArch'" }
}

Function docker_login()
{
    #echo Logging in to Docker registry
    $Password | docker login $Registry -u $Username --password-stdin
    if ($LastExitCode)
    {
        Throw "Docker Login Failed With Exit Code $LastExitCode"
    }
}

Function docker_build_and_tag_and_push(
    # Name of the docker edge image to publish
    [Parameter(Mandatory = $true)]
    [String]$ImageName, 

    # Arch of base image
    [Parameter(Mandatory = $true)]
    [String]$Arch, 

    # Path to the dockerfile
    [ValidateNotNullOrEmpty()]
    [String]$Dockerfile, 

    # Docker context path
    [Parameter(Mandatory = $true)]
    [String]$ContextPath, 

    # Build args
    [String]$BuildArgs
)
{
    $TagPrefix = "$Registry/azedge-$ImageName-windows-${Arch}"
    $FullVersionTag = "${TagPrefix}:$ImageVersion"
    $LatestVersionTag = "${TagPrefix}:latest"

    echo "Building and Pushing Docker image $ImageName for $Arch"
    $docker_build_cmd = "docker build -t $FullVersionTag -t $LatestVersionTag"
    if ($Dockerfile)
    {
        $docker_build_cmd += " --file $Dockerfile"
    }
    $docker_build_cmd += " $ContextPath $BuildArgs"

    echo "Running... $docker_build_cmd"

    Invoke-Expression $docker_build_cmd
    if ($LastExitCode)
    {
        Throw "Docker Build Failed With Exit Code $LastExitCode"
    }

    if (-not $SkipPush)
    {
        docker push $FullVersionTag
        if ($LastExitCode)
        {
            Throw "Docker Push Failed With Exit Code $LastExitCode"
        }

        docker push $LatestVersionTag
        if ($LastExitCode)
        {
            Throw "Docker Push Failed With Exit Code $LastExitCode"
        }
    }
}

Function BuildTagPush([String]$ProjectName, [String]$ProjectPath)
{
    $FullProjectPath = Join-Path -Path $PublishDir -ChildPath $ProjectPath
    docker_build_and_tag_and_push `
        -ImageName $ProjectName `
        -Arch $TargetArch `
        -Dockerfile "$FullProjectPath\docker\windows\$TargetArch\Dockerfile" `
        -ContextPath $FullProjectPath `
        -BuildArgs "--build-arg EXE_DIR=."
}

if (-not $SkipPush)
{
    docker_login
}

BuildTagPush "edge-agent" "Microsoft.Azure.Devices.Edge.Agent.Service"

BuildTagPush "edge-hub" "Microsoft.Azure.Devices.Edge.Hub.Service"

BuildTagPush "edge-service" "Microsoft.Azure.Devices.Edge.Service"

BuildTagPush "simulated-temperature-sensor" "SimulatedTemperatureSensor"

echo "Done Building And Pushing Docker Images"
