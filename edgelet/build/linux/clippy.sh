#!/bin/bash

###############################################################################
# This script runs cargo clippy on your project. This script installs the
# nightly toolchain as it is a dependency of clippy.
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet
SCRIPT_NAME=$(basename "$0")
IMAGE="azureiotedge/cargo-clippy:nightly"
USE_DOCKER=1
RUSTUP="$HOME/.cargo/bin/rustup"
CARGO="$HOME/.cargo/bin/cargo"

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help          Print this help and exit."
    echo " -i, --image         Docker image to run (default: $IMAGE)"
    echo " -d, --use-docker    Run clippy using a docker image (default: Do not run in a docker image)"
    exit 1;
}

function print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

function run_clippy()
{
    echo "Running clippy..."
    (cd $PROJECT_ROOT && $CARGO +nightly clippy --all)
}

function run_clippy_via_docker()
{
    echo "Running clippy docker image..."
    docker run --user "$(id -u)":"$(id -g)" --rm -v "$PROJECT_ROOT:/volume" "$IMAGE"
}
###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            IMAGE="$arg"
            USE_DOCKER=1
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-i" | "--image" ) save_next_arg=1;;
                "-d" | "--use-docker" ) USE_DOCKER=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

if [[ $USE_DOCKER -eq 1 ]]; then
    run_clippy_via_docker
else
    echo "Installing nightly toolchain"
    $RUSTUP install nightly
    echo "Installing clippy..."
    $RUSTUP component add clippy-preview --toolchain=nightly
    run_clippy
fi
