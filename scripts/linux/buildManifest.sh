#!/bin/bash

###############################################################################
# This script builds a multi-architecture manifest image. It expects that the
# the individual images already exist in the destination registry.
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$(realpath $DIR/../..)}
ROOT_FOLDER=$BUILD_REPOSITORY_LOCALPATH

DEFAULT_ARCH='amd64,arm64,arm/v7'
ARCH=$DEFAULT_ARCH
DOCKER_TAGS='[]'
DEFAULT_DOCKER_NAMESPACE='microsoft'
DOCKER_NAMESPACE=$DEFAULT_DOCKER_NAMESPACE
DOCKER_IMAGE_NAME=
IGNORE_MISSING=

###############################################################################
# Check format and content of --arch argument
###############################################################################
check_arch() {
    IFS=',' read -a architectures <<< "$ARCH"
    for arch in ${architectures[@]}
    do
        case "$arch" in
            'amd64'|'arm64'|'arm/v7') ;;
            *) echo "Unsupported architecture '$arch'" && exit 1 ;;
        esac
    done
}

###############################################################################
# Convert from the format of the --arch argument to the format we use in our
# image tags. Docker defines the former (amd64, arm64, or arm/v7; see Docker's
# TARGETARCH automatic variable[1]), we define the latter (amd64, arm64v8, and
# arm32v7).
# [1] https://docs.docker.com/engine/reference/builder/#automatic-platform-args-in-the-global-scope
###############################################################################
convert_arch() {
    arch="$1"
    case "$arch" in
        'amd64') echo 'amd64' ;;
        'arm64') echo 'arm64v8' ;;
        'arm/v7') echo 'arm32v7' ;;
        *) echo "Unsupported architecture '$arch'" && exit 1 ;;
    esac
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
    echo " -r, --registry           Docker registry required to build, tag and run the module"
    echo " -n, --namespace          Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -i, --image-name         Docker image name"
    echo " -v, --image-version      Docker image version. Assumes arch-specific images have the same tag with '-linux-{arch] appended'"
    echo " -a, --arch               Comma-separated list of architectures to combine into multi-arch image (default: $DEFAULT_ARCH)"
    echo "     --tags               Add tags to the docker image. Specify as a JSON array of strings, e.g., --tags '[\"1.0\"]'"
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
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_IMAGE_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            DOCKER_TAGS="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-n" | "--namespace" ) save_next_arg=2;;
                "-i" | "--image-name" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "-a" | "--arch" ) save_next_arg=5;;
                       "--tags" ) save_next_arg=6;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z "$DOCKER_REGISTRY" ]]; then
        echo 'The --registry parameter is required'
        print_help_and_exit
    fi

    if [[ -z "$DOCKER_IMAGE_NAME" ]]; then
        echo 'The --image-name parameter is required'
        print_help_and_exit
    fi

    if [[ -z "$DOCKER_IMAGEVERSION" ]]; then
        echo 'The --image-version parameter is required'
        print_help_and_exit
    fi

    if [[ $(echo "$DOCKER_TAGS" | jq -r '. | type') != 'array' ]]; then
        echo 'The value of --tags must be a JSON array'
        print_help_and_exit
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
process_args $@

image_name="$DOCKER_REGISTRY/$DOCKER_NAMESPACE/$DOCKER_IMAGE_NAME"
arch_digests=()

IFS=',' read -a architectures <<< "$ARCH"
for arch in ${architectures[@]}
do
    image="$image_name:$DOCKER_IMAGEVERSION-linux-$(convert_arch $arch)"
    arch_digests+=( $(docker buildx imagetools inspect $image --format '{{json .Manifest}}' |
        jq --arg arch "$arch" -r '($arch | split("/")) as $parts |
            .manifests[] |
            select(.platform.architecture == $parts[0]) |
            if ($parts | length > 1) then select(.platform.variant == $parts[1]) else . end |
            .digest') )
done

# combine the primary tag (e.g., '1.4.0') and any caller-supplied tags into an array
tags=( $(echo "$DOCKER_TAGS" |
    jq -r --arg primary_tag "$DOCKER_IMAGEVERSION" '. + [ $primary_tag ] | unique | join("\n")') )

# build the multi-arch image with all given tags, using all given arch-specific images as sources
docker buildx imagetools create \
    ${tags[@]/#/--tag $image_name:} \
    "${arch_digests[@]/#/$image_name@}"
