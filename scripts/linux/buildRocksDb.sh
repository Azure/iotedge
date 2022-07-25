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
POSTFIX=
SOURCE_MAP=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo "--output-dir          Path where to put librocksdb folder containing built artifact."
    echo "--postfix             Options: amd64, armhf, arm64."
    echo "--build-number        Build number for which to tag image."
    echo "--arch                Options: amd64, arm32v7, arm64v8."
    echo "--source-map          Path to the JSON file that maps Dockerfile image sources to their replacements. Assumes the tool 'dockersource' is in the PATH"
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
            POSTFIX=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 3 ]; then
            BUILD_NUMBER=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 4 ]; then
            ARCH=$arg
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            SOURCE_MAP="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--output-dir") save_next_arg=1 ;;
            "--postfix") save_next_arg=2 ;;
            "--build-number") save_next_arg=3 ;;
            "--arch") save_next_arg=4 ;;
            "--source-map") save_next_arg=5 ;;
            *) usage ;;
            esac
        fi
    done

    if [[ -n "$SOURCE_MAP" ]] && [[ ! -f "$SOURCE_MAP" ]]; then
        echo "File specified by --source-map does not exist"
        print_help_and_exit
    fi

    if [[ -n "$SOURCE_MAP" ]] && ! command -v dockersource > /dev/null; then
        echo "--source-map specified, but required tool 'dockersource' not found in PATH"
        print_help_and_exit
    fi
}

process_args "$@"

case "$ARCH" in
'amd64') platform='linux/amd64' ;;
'arm32v7') platform='linux/arm/v7' ;;
'arm64v8') platform='linux/arm64' ;;
*) echo "Unrecognized platform '$ARCH'" && exit 1
esac

build_image=rocksdb-build:main-$POSTFIX-$BUILD_NUMBER
mkdir -p $OUTPUT_DIR/librocksdb
cd $BUILD_REPOSITORY_LOCALPATH/edge-util/docker/linux/$ARCH

build_context=
if [[ -n "$SOURCE_MAP" ]]; then
    build_context=$(dockersource --mod-config $SOURCE_MAP ./Dockerfile)
fi

docker buildx create --use --bootstrap
trap "docker buildx rm" EXIT

docker buildx build \
    --load \
    --platform $platform \
    --tag $build_image \
    $([ -z "$build_context" ] || echo $build_context) \
    .

docker run --rm -v $OUTPUT_DIR/librocksdb:/artifacts $build_image cp /publish/librocksdb.so.$POSTFIX /artifacts
