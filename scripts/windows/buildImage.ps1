###############################################################################
# This Script builds a specific Edge application in their respective docker
# containers. This script expects that buildBranch.sh was invoked earlier and
# all the necessary application files and their Dockerfile be published in
# directory identified by environment variable BUILD_BINARIESDIRECTORY
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

    # Project Name
    [Parameter(Mandatory=$true)]
    [String]$Project,

    # Docker Image Name
    [Parameter(Mandatory=$true)]
    [String]$ImageName,

    # Docker Image Version
    [ValidateNotNullOrEmpty()]
    [String]$ImageVersion = $Env:BUILD_BUILDNUMBER,

    # Target architecture
    [ValidateNotNullOrEmpty()]
    [String]$TargetArch = $Env:PROCESSOR_ARCHITECTURE,

    # Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY
    [ValidateNotNullOrEmpty()]
    [String]$BinDir = $Env:BUILD_BINARIESDIRECTORY,

    # Use Windows vNext base images
    [Switch]$vNext,

    # Do not push images to the registry
    [Switch]$SkipPush,

    # Cleanup all images to not pollute the build machine
    [Switch]$Cleanup
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
    "AMD64" { $TargetArch = "amd64" }
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
    [String]$Name,

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
    [String]$BuildArgs,

    [Switch]$Push,

    [String]$Tag
)
{
    $TagPrefix = "$Registry/azureiotedge/$Name-windows-${Arch}"
    $FullVersionTag = "${TagPrefix}:$ImageVersion"
    $LatestVersionTag = "${TagPrefix}:latest"

    echo "Building and Pushing Docker image $Name for $Arch"
    if ($Tag)
    {
        $docker_build_cmd = "docker build --no-cache -t $Tag"
    }
    else
    {
        $docker_build_cmd = "docker build --no-cache -t $FullVersionTag -t $LatestVersionTag"
    }
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

    if ($Push)
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

        docker rmi $FullVersionTag
        docker rmi $LatestVersionTag
    }
}

Function BuildTagPush([String]$ProjectName, [String]$ProjectPath)
{
    $FullProjectPath = Join-Path -Path $PublishDir -ChildPath $ProjectPath
    $Suffix = ""
    if ($vNext)
    {
        $Suffix = ".vnext"
    }

    docker_build_and_tag_and_push `
        -Name $ProjectName `
        -Arch $TargetArch `
        -Dockerfile "$FullProjectPath\docker\windows\$TargetArch\Dockerfile$Suffix" `
        -ContextPath $FullProjectPath `
        -BuildArgs "--build-arg EXE_DIR=." `
        -Push:(-not $SkipPush)
}

if (-not $SkipPush)
{
    docker_login
}

BuildTagPush $ImageName $Project

echo "Done Building And Pushing Docker Images"

if ($Cleanup)
{
    & cmd /c "docker rm $image_name 2>&1"
    $containers = $(docker ps -a -q)
    if ($containers)
    {
        docker rm -f $containers
    }
    docker rmi $(docker images -q)
}
