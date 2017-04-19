#!/bin/bash

###############################################################################
# This Script builds all Edge application in their respective docker containers
# This script expects that buildBranch.sh was invoked earlier and all the
# necessary application files and their Dockerfile be published in
# directory identified by environement variable BUILD_BINARIESDIRECTORY
###############################################################################

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "Missing arguments. Usage: $0 -r <registry> -u <username> -p <password> [-v <docker image version=build number]"
    exit 1;
}

###############################################################################
# Validate Environment Variables
###############################################################################
TMP=${BUILD_BINARIESDIRECTORY:?Env variable BUILD_BINARIESDIRECTORY needs to be set and be non-empty}

if [ ! -d "$BUILD_BINARIESDIRECTORY" ]; then
    echo "Path $BUILD_BINARIESDIRECTORY does not exist"
    exit 1
fi

###############################################################################
# Check if the underlying architecture is supported
###############################################################################
ARCH=$(uname -m)
if [ "$ARCH" == "x86_64" ]; then
    ARCH="x64"
elif [ "$ARCH" == "armv7l" ]; then
    ARCH="armv7hf"
else
    echo "Unsupported Architecture"
    exit 1
fi

###############################################################################
# Setup Global Variables
###############################################################################
PUBLISH_DIR=$BUILD_BINARIESDIRECTORY/publish

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
while getopts ":r:u:p:v:" o; do
    case "${o}" in
        r)
            DOCKER_REGISTRY=${OPTARG}
            ;;
        u)
            DOCKER_USERNAME=${OPTARG}
            ;;
        p)
            DOCKER_PASSWORD=${OPTARG}
            ;;
        v)
            DOCKER_IMAGEVERSION=${OPTARG}
            ;;
        *)
            usage
            ;;
    esac
done
shift $((OPTIND-1))

if [ -z "${DOCKER_REGISTRY}" ] || [ -z "${DOCKER_USERNAME}" ] || [ -z "${DOCKER_PASSWORD}" ]; then
    usage
fi

if [ -z "${DOCKER_IMAGEVERSION}" ]; then
    if [ ! -z "${BUILD_BUILDNUMBER}" ]; then
        DOCKER_IMAGEVERSION=$BUILD_BUILDNUMBER
    else
        echo "Error: Docker image version not found. Either set BUILD_BUILDNUMBER environment variable, or pass in -v parameter."
        exit 1
    fi
fi

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
docker_build_and_tag_and_push()
{
    imagename="$1"
    arch="$2"
    dockerfile="$3"
    context_path="$4"
    build_args="$5"

    if [ -z "${imagename}" ] || [ -z "${arch}" ] || [ -z "${context_path}" ]; then
        echo "Error: Arguments are invalid [$imagename] [$arch] [$context_path]"
        exit 1
    fi

    echo "Building and Pushing Docker image $imagename for $arch"
    docker_build_cmd="docker build"
    docker_build_cmd+=" -t $DOCKER_REGISTRY/azedge-$imagename-$arch:$DOCKER_IMAGEVERSION"
    docker_build_cmd+=" -t $DOCKER_REGISTRY/azedge-$imagename-$arch:latest"
    if [ ! -z "${dockerfile}" ]; then
        docker_build_cmd+=" --file $dockerfile"
    fi
    docker_build_cmd+=" $context_path $build_args"

    echo "Running... $docker_build_cmd"

    $docker_build_cmd
    cmd_output=$?

    if [ ${cmd_output} -ne 0 ]; then
        echo "Docker Build Failed With Exit Code $cmd_output"
        exit 1
    else
        docker push $DOCKER_REGISTRY/azedge-$imagename-$arch:$DOCKER_IMAGEVERSION
        cmd_output=$?
        if [ ${cmd_output} -ne 0 ]; then
            echo "Docker Build Failed With Exit Code $cmd_output"
            exit 1
        else
            docker push $DOCKER_REGISTRY/azedge-$imagename-$arch:latest
            cmd_output=$?
            if [ ${cmd_output} -ne 0 ]; then
                echo "Docker Push Latest Image Failed: $cmd_output"
                exit 1
            fi
        fi
    fi

    return $cmd_output
}

#echo Logging in to Docker registry
docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
if [ $? -ne 0 ]; then
    echo "Docker Login Failed!"
    exit 1
fi

# push edge-runtime dotnet image
EXE_DIR="dotnet-runtime"
EXE_DOCKER_DIR=$PUBLISH_DIR/docker/$EXE_DIR/latest
docker_build_and_tag_and_push $EXE_DIR "$ARCH" "$EXE_DOCKER_DIR/$ARCH/Dockerfile" "$EXE_DOCKER_DIR" ""

# push edge-agent image
EXE_DIR="Microsoft.Azure.Devices.Edge.Agent.Service"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push edge-agent "$ARCH" "$EXE_DOCKER_DIR/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."

# push edge-hub image
EXE_DIR="Microsoft.Azure.Devices.Edge.Hub.Service"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push edge-hub "$ARCH" "$EXE_DOCKER_DIR/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."

echo "Done Building And Pushing Docker Images"
