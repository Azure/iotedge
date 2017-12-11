#!/bin/bash

###############################################################################
# This script creates an ARM docker image as a base for the edgeHub module.
# then pushes it to the appropriate registries.
# It assumes that the caller is logged into registries:
# edgebuilds.azurecr.io
# edgerelease.azurecr.io
# hub.docker.com
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
SCRIPT_NAME=$(basename $0)
PUBLISH_DIR=
DOCKERFILE=
DOCKER_IMAGENAME=
DEFAULT_DOCKER_NAMESPACE="azureiotedge"
DOCKER_NAMESPACE=$DEFAULT_DOCKER_NAMESPACE
BUILD_DOCKERFILEDIR=
DEFAULT_DOCKER_IMAGEVERSION=1.0-preview
DOCKER_IMAGEVERSION=$DEFAULT_DOCKER_IMAGEVERSION

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [ "$ARCH" == "armv7l" ]; then
        ARCH="arm32v7"
    else
        echo "Unsupported architecture"
        exit 1
    fi
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: You might have to run this as root or sudo."
    echo "Note: This script is only applicable on ARM architectures."
    echo ""
    echo " -i, --image-name     Image name (azureiotedge-module-base, azureiotedge-agent-base, or azureiotedge-hub-base)"
    echo " -d, --project-dir    Project directory (required)."
    echo "                      Directory which contains docker/linux/arm32v7/base/Dockerfile"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -v, --image-version  Docker Image Version. (default: $DEFAULT_DOCKER_IMAGEVERSION)"

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
            PUBLISH_DIR="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-d" | "--project-dir" ) save_next_arg=1;;
                "-i" | "--image-name" ) save_next_arg=2;;
                "-n" | "--namespace" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;

                * ) usage;;
            esac
        fi
    done

    if [[ -z ${PUBLISH_DIR} ]]; then
        echo "Docker project directory parameter invalid"
        print_help_and_exit
    fi


    if [[ -z ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name parameter invalid"
        print_help_and_exit
    fi

    if [[ "azureiotedge-module-base" != ${DOCKER_IMAGENAME} ]] && [[ "azureiotedge-hub-base" != ${DOCKER_IMAGENAME} ]] && [[ "azureiotedge-agent-base" != ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name must be azureiotedge-module-base or azureiotedge-hub-base"
        print_help_and_exit
    fi

    if [[ ! -d $PUBLISH_DIR ]]; then
        echo "Publish directory does not exist or is invalid"
        print_help_and_exit
    fi


    BUILD_DOCKERFILEDIR=$PUBLISH_DIR/docker/linux/$ARCH/base
    if [[ -z ${BUILD_DOCKERFILEDIR} ]] || [[ ! -d ${BUILD_DOCKERFILEDIR} ]]; then
        echo "No directory for ARM base images in $BUILD_DOCKERFILEDIR"
        print_help_and_exit
    fi

    DOCKERFILE="$BUILD_DOCKERFILEDIR/Dockerfile"
    if [[ ! -f $DOCKERFILE ]]; then
        echo "No Dockerfile at $DOCKERFILE"
        print_help_and_exit
    fi
}
###############################################################################
# Build docker image and push it to private repo
#
#   @param[1] - imagename; Name of the docker edge image to publish; Required;
#   @param[2] - arch; Arch of base image; Required;
#   @param[3] - dockerfile; Path to the dockerfile; Required;
#   @param[4] - context_path; docker context path; Required;
#   @param[5] - build_args; docker context path; Optional;
#               Leave as "" and no build args will be supplied.
#   @param[6] - registry
###############################################################################
docker_build_and_tag_and_push()
{
    imagename="$1"
    arch="$2"
    dockerfile="$3"
    context_path="$4"
    build_args="$5"
    registry="$6"

    if [ -z "${imagename}" ] || [ -z "${arch}" ] || [ -z "${context_path}" ] || [ -z "${dockerfile}" ]; then
        echo "Error: Arguments are invalid [$imagename] [$arch] [$dockerfile] [$context_path]"
        exit 1
    fi

    echo "Building and pushing Docker image $imagename for $arch"
    docker_build_cmd="docker build --no-cache"
    docker_build_cmd+=" -t $registry/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
    docker_build_cmd+=" --file $dockerfile"
    docker_build_cmd+=" $context_path $build_args"

    echo "Running... $docker_build_cmd"

    $docker_build_cmd

    if [ $? -ne 0 ]; then
        echo "Docker build failed with exit code $?"
        exit 1
    else
        docker push $registry/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch
        if [ $? -ne 0 ]; then
            echo "Docker push failed with exit code $?"
            exit 1
        fi
    fi

    return $?
}

move_image()
{
    imagename="$1"
    arch="$2"
    dockerfile="$3"
    context_path="$4"
    from_registry="$5"
    to_registry="$6"

    FROM_IMAGE="$from_registry/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
    if [ -z "$from_registry" ]; then
        FROM_IMAGE="$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
    fi

    TO_IMAGE="$to_registry/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
    if [ -z "$to_registry" ]; then
        TO_IMAGE="$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
    fi
    echo "Pulling $FROM_IMAGE"
    docker pull $FROM_IMAGE
    [ $? -eq 0 ] || exit $?

    echo "Tagging image: $TO_IMAGE"
    docker tag $FROM_IMAGE $TO_IMAGE
    [ $? -eq 0 ] || exit $?

    echo "Pushing image: $TO_IMAGE"
    docker push $TO_IMAGE
    [ $? -eq 0 ] || exit $?
}
###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args "$@"


# push image for edge-hub to edgebuilds

docker_build_and_tag_and_push "$DOCKER_IMAGENAME" "$ARCH" "$DOCKERFILE" "$BUILD_DOCKERFILEDIR" "" "edgebuilds.azurecr.io"
[ $? -eq 0 ] || exit $?

# push image for edge-hub to edgerelease

move_image "$DOCKER_IMAGENAME" "$ARCH" "$DOCKERFILE" "$BUILD_DOCKERFILEDIR" "edgebuilds.azurecr.io" "edgerelease.azurecr.io"
[ $? -eq 0 ] || exit $?

# push image to docker hub
move_image "$DOCKER_IMAGENAME" "$ARCH" "$DOCKERFILE" "$BUILD_DOCKERFILEDIR" "edgebuilds.azurecr.io" ""
[ $? -eq 0 ] || exit $?

echo "Done building and pushing Docker image $DOCKER_IMAGENAME for ARM base images"

[ $? -eq 0 ] || exit $?
