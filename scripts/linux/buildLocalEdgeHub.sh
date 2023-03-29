#!/bin/bash

###############################################################################
# This script builds an EdgeHub image locally
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
    echo "--registry-address    Docker registry where image will be published."
    echo "--version             Tag for built edge hub image."
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
            REGISTRY_ADDRESS=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 2 ]; then
            VERSION=$arg
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--registry-address") save_next_arg=1 ;;
            "--version") save_next_arg=2 ;;
            *) usage ;;
            esac
        fi
    done
}

process_args "$@"

scripts/linux/buildBranch.sh --no-rocksdb-bin --skip-quickstart

scripts/linux/buildRocksDb.sh \
    --output-dir target/publish/Microsoft.Azure.Devices.Edge.Hub.Service \
    --build-number debug \
    --arch amd64

scripts/linux/buildImage.sh \
    --app Microsoft.Azure.Devices.Edge.Hub.Service \
    --bin target \
    --name azureiotedge-hub \
    --platforms linux/amd64 \
    --registry "$REGISTRY_ADDRESS" \
    --version "$VERSION"
