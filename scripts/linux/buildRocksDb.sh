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
SOURCE_MAP=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo "--output-dir          Path to librocksdb folder that contains resulting binaries."
    echo "--build-number        Build number for which to tag image."
    echo "--arch                Options: amd64, arm64, arm/v7."
    echo "--source-map          Path to the JSON file that maps Dockerfile image sources to their replacements. Assumes the tool 'gnarly' is in the PATH"
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
    local save_next_arg=0
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
        elif [[ ${save_next_arg} -eq 4 ]]; then
            SOURCE_MAP="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--output-dir") save_next_arg=1 ;;
            "--build-number") save_next_arg=2 ;;
            "--arch") save_next_arg=3 ;;
            "--source-map") save_next_arg=4 ;;
            *) usage ;;
            esac
        fi
    done

    if [[ -n "$SOURCE_MAP" ]] && [[ ! -f "$SOURCE_MAP" ]]; then
        echo "File specified by --source-map does not exist"
        print_help_and_exit
    fi

    if [[ -n "$SOURCE_MAP" ]] && ! command -v gnarly > /dev/null; then
        echo "--source-map specified, but required tool 'gnarly' not found in PATH"
        print_help_and_exit
    fi
}

process_args "$@"

case "$ARCH" in
    'amd64'|'arm64'|'arm/v7') ;;
    *) echo "Unrecognized architecture '$ARCH'" && exit 1
esac

build_image=rocksdb-build:main-${ARCH/\//}-$BUILD_NUMBER
mkdir -p $OUTPUT_DIR/librocksdb
cd $BUILD_REPOSITORY_LOCALPATH/edge-util/docker/linux

build_context=
if [[ -n "$SOURCE_MAP" ]]; then
    build_context=$(gnarly --mod-config $SOURCE_MAP ./Dockerfile)
fi

docker buildx create --use --bootstrap
trap "docker buildx rm" EXIT

docker buildx build \
    --load \
    --platform "linux/$ARCH" \
    --tag $build_image \
    $([ -z "$build_context" ] || echo $build_context) \
    .

docker run --rm -v $OUTPUT_DIR/librocksdb:/artifacts $build_image \
    mkdir -p /artifacts/$ARCH && cp /publish/$ARCH/librocksdb.so /artifacts/$ARCH/
