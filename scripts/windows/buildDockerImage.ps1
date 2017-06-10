Param(
    $DOTNET_DOWNLOAD_URL,
    $DOCKER_REGISTRY = "edgebuilds.azurecr.io",
    $DOCKER_IMAGEVERSION = "1000"
)

$BUILD_BINARIESDIRECTORY = "target"
$PUBLISH_DIR = Join-Path $BUILD_BINARIESDIRECTORY "publish"

switch ($Env:PROCESSOR_ARCHITECTURE)
{
    "AMD64" { $arch = "x64" }
    default { throw "Unsupported arch" }
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
    echo "Building and Pushing Docker image $imagename for $arch"
    $docker_build_cmd="docker build"
    $docker_build_cmd+=" -t $DOCKER_REGISTRY/azedge-$imagename-windows-${arch}:$DOCKER_IMAGEVERSION"
    $docker_build_cmd+=" -t $DOCKER_REGISTRY/azedge-$imagename-windows-${arch}:latest"
    if ($dockerfile)
    {
        $docker_build_cmd+=" --file $dockerfile"
    }
    $docker_build_cmd+=" $context_path $build_args"

    echo "Running... $docker_build_cmd"

    Invoke-Expression $docker_build_cmd

    if ($LastExitCode)
    {
        Throw "Docker Build Failed With Exit Code $LastExitCode"
    }
    else
    {
        <#
        docker push $DOCKER_REGISTRY/azedge-$imagename-$arch:$DOCKER_IMAGEVERSION
        if [ $? -ne 0 ]; then
            echo "Docker Build Failed With Exit Code $?"
            exit 1
        else
            docker push $DOCKER_REGISTRY/azedge-$imagename-$arch:latest
            if [ $? -ne 0 ]; then
                echo "Docker Push Latest Image Failed: $?"
                exit 1
            fi
        fi
        #>
    }

    return $?
}

# push edge-agent image
$EXE_DIR = "Microsoft.Azure.Devices.Edge.Agent.Service"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push edge-agent "$ARCH" "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" "$DOTNET_BUILD_ARG"

# push edge-hub image
$EXE_DIR = "Microsoft.Azure.Devices.Edge.Hub.Service"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push edge-hub "$ARCH" "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" "$DOTNET_BUILD_ARG"

# push SimulatedTemperatureSensor image
$EXE_DIR = "SimulatedTemperatureSensor"
$EXE_DOCKER_DIR = "$PUBLISH_DIR\$EXE_DIR\docker"
$DOTNET_BUILD_ARG = "--build-arg EXE_DIR=."
docker_build_and_tag_and_push simulated-temperature-sensor "$ARCH" "$EXE_DOCKER_DIR\windows\$ARCH\Dockerfile" "$PUBLISH_DIR\$EXE_DIR" "$DOTNET_BUILD_ARG"

echo "Done Building And Pushing Docker Images"
