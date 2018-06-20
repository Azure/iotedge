#!/bin/bash

###############################################################################
# Creates the base amd64 docker image used by the Edge Hub and Edge Agent
# module images, and pushes it to the following registries:
#   edgebuilds.azurecr.io
#   edgerelease.azurecr.io
#   docker.io
# It assumes the caller is already logged into these registries.
###############################################################################

ARCH=$(uname -m)
ROOT_DIR=$(cd "$(dirname "$0")/../.." && pwd)

DOCKERFILE_DIR=
IMAGE_NAME=azureiotedge-runtime-base
IMAGE_VERSION=
NAMESPACE=azureiotedge
NO_PUSH=0
declare -a IMAGE_TAGS

check_arch()
{
    if [ "$ARCH" == "x86_64" ]; then
        ARCH="amd64"
    else
        echo "Unsupported architecture"
        exit 1
    fi
}

usage()
{
    SCRIPT_NAME=$(basename $0)
    echo "$SCRIPT_NAME [options]"
    echo "Note: You might have to run this as root or sudo."
    echo "Note: This script only runs on amd64."
    echo ""
    echo " -v, --image-version  Docker Image Version. (required)"
    echo "     --no-push        Build/tag only; don't push image to container registry"

    exit 1;
}

process_args()
{
    save_next_arg=0
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            IMAGE_VERSION="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-v" | "--image-version" ) save_next_arg=1;;
                "--no-push" ) NO_PUSH=1;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${IMAGE_VERSION} ]]; then
        echo "No Docker image version given\n"
        usage
    fi

    DOCKERFILE_DIR=$ROOT_DIR/edge-util/docker/linux/$ARCH
    if [[ ! -f $DOCKERFILE_DIR/Dockerfile ]]; then
        echo "No Dockerfile at $DOCKERFILE_DIR\n"
        usage
    fi

    registries=("edgebuilds.azurecr.io/" "edgerelease.azurecr.io/" "")
    IMAGE_TAGS=("${registries[@]/%/$NAMESPACE/$IMAGE_NAME:$IMAGE_VERSION-linux-$ARCH}")
}

docker_build_and_tag()
{
    tags=("${IMAGE_TAGS[@]/#/-t }") # prefix each tag with the -t option for 'docker build'

    cmd="docker build --no-cache"
    cmd+=" ${tags[@]}"
    cmd+=" $DOCKERFILE_DIR"

    echo -e "COMMAND\n $cmd\n"

    $cmd
}

docker_push()
{
    for tag in "${IMAGE_TAGS[@]}"
    do
        echo -e "COMMAND\n docker push $tag\n"
        docker push $tag
        [ $? -eq 0 ] || exit $?
    done
}

check_arch
process_args "$@"

docker_build_and_tag
[ $? -eq 0 ] || exit $?

if [ $NO_PUSH -eq 0 ]; then
    docker_push
    [ $? -eq 0 ] || exit $?
fi

echo "Done"
