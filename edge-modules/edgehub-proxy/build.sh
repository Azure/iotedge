#!/bin/bash

###############################################################################
# This script builds a static binary of the edgehub-proxy
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

SCRIPT_NAME=$(basename "$0")
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}

IMAGE=edgehub-proxy:latest

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -i,  --image                  Toolchain (default: stable)"
    exit 1;
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            IMAGE="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-i" | "--image" ) save_next_arg=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"


###############################################################################
# Build
###############################################################################

echo ${PROJECT_ROOT}
docker run --rm -it -v "${PROJECT_ROOT}":/home/rust/src ekidd/rust-musl-builder cargo build --release --manifest-path /home/rust/src/edge-modules/edgehub-proxy/Cargo.toml

strip ${PROJECT_ROOT}/edge-modules/edgehub-proxy/target/x86_64-unknown-linux-musl/release/edgehub-proxy

docker build -t ${IMAGE} ${PROJECT_ROOT}/edge-modules/edgehub-proxy/
