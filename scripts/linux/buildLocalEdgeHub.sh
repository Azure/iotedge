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
    echo "--registry-address              Path where to put librocksdb folder containing built artifact."
    echo "--version                       Tag for built edge hub image."
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

scripts/linux/buildBranch.sh
scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path mqtt/mqttd
scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path edge-hub/watchdog
scripts/linux/consolidate-build-artifacts.sh --artifact-name "edge-hub"
scripts/linux/buildRocksDb.sh --output-dir $(pwd)/target/publish/edge-hub --postfix amd64 --build-number debug --arch amd64
scripts/linux/buildImage.sh -r $REGISTRY_ADDRESS -i "azureiotedge-hub" -n "microsoft" -P "edge-hub" -v $VERSION --bin-dir "target"
