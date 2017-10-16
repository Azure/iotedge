#!/bin/bash

###############################################################################
# This Script builds all Edge application in their respective docker containers
# This script expects that buildBranch.sh was invoked earlier and all the
# necessary application files and their Dockerfile be published in
# directory identified by environement variable BUILD_BINARIESDIRECTORY
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
SCRIPT_NAME=$(basename $0)
PUBLISH_DIR=
DOTNET_DOWNLOAD_URL=
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:=""}

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [ "$ARCH" == "x86_64" ]; then
        ARCH="amd64"
    elif [ "$ARCH" == "armv7l" ]; then
        ARCH="arm32v7"
    else
        echo "Unsupported Architecture"
        exit 1
    fi
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -u, --username       Docker Registry Username"
    echo " -p, --password       Docker Username's password"
    echo " -v, --image-version  Docker Image Version. Either use this option or set env variable BUILD_BUILDNUMBER"
    echo " -t, --target-arch    Target architecture (default: uname -m)"
    echo "--bin-dir             Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
    echo "--dotnet-url          Dotnet Runtime Download (tar.gz) URL"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DOCKER_USERNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_PASSWORD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            DOTNET_DOWNLOAD_URL="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-u" | "--username" ) save_next_arg=2;;
                "-p" | "--password" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "--bin-dir" ) save_next_arg=5;;
                "--dotnet-url" ) save_next_arg=6;;
                "-t" | "--target-arch" ) save_next_arg=7;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${DOCKER_REGISTRY} ]]; then
        echo "Registry Parameter Invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_USERNAME} ]]; then
        echo "Docker Username Parameter Invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_PASSWORD} ]]; then
        echo "Docker Password Parameter Invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_IMAGEVERSION} ]]; then
        if [ ! -z "${BUILD_BUILDNUMBER}" ]; then
            DOCKER_IMAGEVERSION=$BUILD_BUILDNUMBER
        else
            echo "Docker image version not found."
            print_help_and_exit
        fi
    fi
    
    if [[ -z ${BUILD_BINARIESDIRECTORY} ]] || [[ ! -d ${BUILD_BINARIESDIRECTORY} ]]; then
        echo "Bin directory does not exist or is invalid"
        print_help_and_exit
    fi

    PUBLISH_DIR=$BUILD_BINARIESDIRECTORY/publish

    if [[ ! -d $PUBLISH_DIR ]]; then
        echo "Publish directory does not exist or is invalid"
        print_help_and_exit
    fi
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
    docker_build_cmd="docker build --no-cache"
    docker_build_cmd+=" -t $DOCKER_REGISTRY/azureiotedge/$imagename-linux-$arch:$DOCKER_IMAGEVERSION"
    docker_build_cmd+=" -t $DOCKER_REGISTRY/azureiotedge/$imagename-linux-$arch:latest"
    if [ ! -z "${dockerfile}" ]; then
        docker_build_cmd+=" --file $dockerfile"
    fi
    docker_build_cmd+=" $context_path $build_args"

    echo "Running... $docker_build_cmd"

    $docker_build_cmd

    if [ $? -ne 0 ]; then
        echo "Docker Build Failed With Exit Code $?"
        exit 1
    else
        docker push $DOCKER_REGISTRY/azureiotedge/$imagename-linux-$arch:$DOCKER_IMAGEVERSION
        if [ $? -ne 0 ]; then
            echo "Docker Build Failed With Exit Code $?"
            exit 1
        else
            docker push $DOCKER_REGISTRY/azureiotedge/$imagename-linux-$arch:latest
            if [ $? -ne 0 ]; then
                echo "Docker Push Latest Image Failed: $?"
                exit 1
            fi
        fi
    fi

    return $?
}

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args $@

#echo Logging in to Docker registry
docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
if [ $? -ne 0 ]; then
    echo "Docker Login Failed!"
    exit 1
fi

# push edge-agent image
EXE_DIR="Microsoft.Azure.Devices.Edge.Agent.Service"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push edge-agent "$ARCH" "$EXE_DOCKER_DIR/linux/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."
[ $? -eq 0 ] || exit $?

# push edge-hub image
EXE_DIR="Microsoft.Azure.Devices.Edge.Hub.Service"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push edge-hub "$ARCH" "$EXE_DOCKER_DIR/linux/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."
[ $? -eq 0 ] || exit $?

# push edge-service image
EXE_DIR="Microsoft.Azure.Devices.Edge.Service"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push edge-service "$ARCH" "$EXE_DOCKER_DIR/linux/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."
[ $? -eq 0 ] || exit $?

# push SimulatedTemperatureSensor image
EXE_DIR="SimulatedTemperatureSensor"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push simulated-temperature-sensor "$ARCH" "$EXE_DOCKER_DIR/linux/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."
[ $? -eq 0 ] || exit $?

# push FunctionsBinding image
EXE_DIR="Microsoft.Azure.Devices.Edge.Functions.Binding"
EXE_DOCKER_DIR=$PUBLISH_DIR/$EXE_DIR/docker
docker_build_and_tag_and_push functions-binding "$ARCH" "$EXE_DOCKER_DIR/linux/$ARCH/Dockerfile" "$PUBLISH_DIR/$EXE_DIR" "--build-arg EXE_DIR=."
[ $? -eq 0 ] || exit $?

echo "Done Building And Pushing Docker Images"

[ $? -eq 0 ] || exit $?
