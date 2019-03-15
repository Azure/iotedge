#!/bin/bash

###############################################################################
# This script creates an ARM docker image as a base for the edgeHub module.
# then pushes it to the appropriate registries.
# It assumes that the caller is logged into registries:
# edgebuilds.azurecr.io
# edgerelease.azurecr.io
# hub.docker.com
###############################################################################

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
DOCKER_IMAGEVERSION=
NO_PUSH=0
declare -a DOCKER_IMAGE_TAGS

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [ "$ARCH" == "armv7l" ]; then
        ARCH="arm32v7"
    elif [ "$ARCH" == "aarch64" ]; then
        ARCH="arm64v8"
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
    echo "                      Directory which contains docker/linux/arm32v7/base/Dockerfile or docker/linux/arm64v8/base/Dockerfile"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -v, --image-version  Docker Image Version. (required)"
    echo "     --no-push        Build/tag only; don't push image to container registries"

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
                "--no-push" ) NO_PUSH=1;;
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

    if [[ -z ${DOCKER_IMAGEVERSION} ]]; then
        echo "Docker image version parameter invalid"
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

    registries=("edgebuilds.azurecr.io/" "edgerelease.azurecr.io/" "")
    DOCKER_IMAGE_TAGS=("${registries[@]/%/$DOCKER_NAMESPACE/$DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION-linux-$ARCH}")
}

###############################################################################
# Build docker image and tag it once for each registry
#
#   @param[1] - build_args; Optional
###############################################################################
docker_build_and_tag()
{
    build_args="$1"

    tags=("${DOCKER_IMAGE_TAGS[@]/#/-t }") # prefix each tag with the -t option for 'docker build'

    docker_build_cmd="docker build --no-cache"
    docker_build_cmd+=" ${tags[@]}"
    docker_build_cmd+=" --file $DOCKERFILE"
    docker_build_cmd+=" $BUILD_DOCKERFILEDIR"
    docker_build_cmd+=" $build_args"

    echo -e "COMMAND\n $docker_build_cmd\n"

    $docker_build_cmd
}

docker_push()
{
    for tag in "${DOCKER_IMAGE_TAGS[@]}"
    do
        echo -e "COMMAND\n docker push $tag\n"
        docker push $tag
        [ $? -eq 0 ] || exit $?
    done
}

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args "$@"

docker_build_and_tag
[ $? -eq 0 ] || exit $?

if [ $NO_PUSH -eq 0 ]; then
    docker_push
    [ $? -eq 0 ] || exit $?
fi

echo "Done"