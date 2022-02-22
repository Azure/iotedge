#!/bin/bash

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

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo "--output-dir                   Path where to put librocksdb folder containing built artifact."
    echo "--postfix                       Options: amd64, armhf, arm64."
    echo "--build-number                  Build number for which to tag image."
    echo "--arch                          Options: amd64, arm32v7, arm64v8."
    echo " -h, --help                     Print this help and exit."
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
            POSTFIX=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 3 ]; then
            BUILD_NUMBER=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 4 ]; then
            ARCH=$arg
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--output-dir") save_next_arg=1 ;;
            "--postfix") save_next_arg=2 ;;
            "--build-number") save_next_arg=3 ;;
            "--arch") save_next_arg=4 ;;
            *) usage ;;
            esac
        fi
    done
}

process_args "$@"

build_image=rocksdb-build:main-$POSTFIX-$BUILD_NUMBER
mkdir -p $OUTPUT_DIR/librocksdb
cd $BUILD_REPOSITORY_LOCALPATH/edge-util/docker/linux/$ARCH
docker build --tag ${build_image} .
docker run --rm -v $OUTPUT_DIR/librocksdb:/artifacts ${build_image} cp /publish/librocksdb.so.$POSTFIX /artifacts
