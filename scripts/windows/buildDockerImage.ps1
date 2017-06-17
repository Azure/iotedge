Param(
    $DOTNET_DOWNLOAD_URL,
    $DOCKER_REGISTRY = "edgebuilds.azurecr.io",
    $DOCKER_IMAGEVERSION = "1000",
    $DOCKER_USERNAME,
    $DOCKER_PASSWORD
)

$BUILD_BINARIESDIRECTORY = "target"
$PUBLISH_DIR = Join-Path $BUILD_BINARIESDIRECTORY "publish"

switch ($Env:PROCESSOR_ARCHITECTURE)
{
    "AMD64" { $arch = "x64" }
    default { throw "Unsupported arch" }
}

Function docker_login()
{
    #echo Logging in to Docker registry
    docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
    if ($LastExitCode)
    {
        Throw "Docker Login Failed With Exit Code $LastExitCode"
    }
}

###############################################################################
# Build docker image and push it to private repo
#
#   @param[1] - imagename; Name of the docker edge image to publish; Required;
#   @param[2] - arch; Arch of base image; Required;
#   @param[3] - dockerfile; Path to the dockerfile; Optional;
#               Leave as "" and defaults will be chosen.
#   @param[4] - context_path; docker context path; Required;
#   @param[5] - build_args; docker context path; Optional;
#               Leave as "" and no build args will be supplied.
###############################################################################
Function docker_build_and_tag_and_push(
    [Parameter(Mandatory = $true)]$imagename, 
    [Parameter(Mandatory = $true)]$arch, 
    $dockerfile, 
    [Parameter(Mandatory = $true)]$context_path, 
    $build_args)
{
    $FullVersionTag = "$DOCKER_REGISTRY/azedge-$imagename-windows-${arch}:$DOCKER_IMAGEVERSION"
    $LatestVersionTag = "$DOCKER_REGISTRY/azedge-$imagename-windows-${arch}:latest"

    echo "Building and Pushing Docker image $imagename for $arch"
    $docker_build_cmd = "docker build -t $FullVersionTag -t $LatestVersionTag"
    if ($dockerfile)
    {
        $docker_build_cmd += " --file $dockerfile"
    }
    $docker_build_cmd += " $context_path $build_args"

    echo "Running... $docker_build_cmd"

    Invoke-Expression $docker_build_cmd

    if ($LastExitCode)
    {
        Throw "Docker Build Failed With Exit Code $LastExitCode"
    }
    else
    {
        if ($PUSH)
        {
            docker push $FullVersionTag
            if ($LastExitCode)
            {
                Throw "Docker Push Failed With Exit Code $LastExitCode"
            }
            else
            {
                docker push $LatestVersionTag
                if ($LastExitCode)
                {
                    Throw "Docker Push Failed With Exit Code $LastExitCode"
                }
            }
        }
    }
}

if ($DOCKER_USERNAME -and $DOCKER_PASSWORD)
{
    docker_login
    $PUSH = $true
}

# push edge-agent image
$EXE_DIR = "Microsoft.Azure.Devices.Edge.Agent.Service"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push edge-agent $ARCH "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" $DOTNET_BUILD_ARG

# push edge-hub image
$EXE_DIR = "Microsoft.Azure.Devices.Edge.Hub.Service"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push edge-hub $ARCH "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" $DOTNET_BUILD_ARG

# push edge-service image
$EXE_DIR = "Microsoft.Azure.Devices.Edge.Service"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push edge-service $ARCH "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" $DOTNET_BUILD_ARG

# push SimulatedTemperatureSensor image
$EXE_DIR = "SimulatedTemperatureSensor"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push simulated-temperature-sensor $ARCH "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" $DOTNET_BUILD_ARG

echo "Done Building And Pushing Docker Images"
