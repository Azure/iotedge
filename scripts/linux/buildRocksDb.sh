#!/bin/bash
set -euo pipefail

###############################################################################
# This script builds cross compiles rocksdb .so file using docker buildx
###############################################################################

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
BUILD_REPOSITORY_LOCALPATH=$(realpath ${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..})
SCRIPT_NAME=$(basename "$0")

ARCH=
BUILD_NUMBER=
OUTPUT_DIR=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo "--output-dir          Path to librocksdb folder that contains resulting binaries."
    echo "--build-number        Build number for which to tag image."
    echo "--arch                Options: amd64, arm32v7, arm64v8."
    echo " -h, --help           Print this help and exit."
    exit 1
}

function print_help_and_exit() {
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args() {
    save_next_arg=0
    for arg in "$@"; do
        if [ ${save_next_arg} -eq 1 ]; then
            OUTPUT_DIR=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 2 ]; then
            BUILD_NUMBER=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 3 ]; then
            ARCH=$arg
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--output-dir") save_next_arg=1 ;;
            "--build-number") save_next_arg=2 ;;
            "--arch") save_next_arg=3 ;;
            *) usage ;;
            esac
        fi
    done

    if [ ! -d "$OUTPUT_DIR" ]; then
        echo "Value '$OUTPUT_DIR' specified by --output-dir is not a valid directory"
        print_help_and_exit
    fi

    OUTPUT_DIR=$(realpath $OUTPUT_DIR)
}

process_args "$@"

case "$ARCH" in
    'amd64') platform='linux/amd64' ;;
    'arm32v7') platform='linux/arm/v7' ;;
    'arm64v8') platform='linux/arm64' ;;
    *) echo "Unrecognized platform '$ARCH'" && exit 1
esac

build_image=rocksdb-build:main-$ARCH-$BUILD_NUMBER
cd $BUILD_REPOSITORY_LOCALPATH/edge-util/docker/linux

docker buildx create --use --bootstrap
trap "docker buildx rm" EXIT

docker buildx build \
    --load \
    --platform $platform \
    --tag $build_image \
    .

docker run \
    --rm \
    --platform $platform \
    -v $OUTPUT_DIR/librocksdb/$platform:/artifacts/$platform \
    $build_image \
    cp /publish/$platform/librocksdb.so /artifacts/$platform/
