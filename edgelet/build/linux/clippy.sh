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
TOOLCHAIN='nightly-2018-09-12'
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
    (cd $PROJECT_ROOT && $CARGO "+$TOOLCHAIN" clippy --all)
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
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

if [[ $USE_DOCKER -eq 1 ]]; then
    run_clippy_via_docker
else
    echo "Installing $TOOLCHAIN toolchain"
    $RUSTUP install "$TOOLCHAIN"
    echo "Installing clippy..."
    $RUSTUP component add clippy-preview "--toolchain=$TOOLCHAIN"
    run_clippy
fi
