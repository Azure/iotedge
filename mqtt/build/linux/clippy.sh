#!/bin/bash

###############################################################################
# This script runs cargo clippy on your project. 
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/mqtt
SCRIPT_NAME=$(basename "$0")
RUSTUP="${CARGO_HOME:-"$HOME/.cargo"}/bin/rustup"
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"

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

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args()
{
    for arg in "$@"
    do
        case "$arg" in
            "-h" | "--help" ) usage;;
            * ) usage;;
        esac
    done
}

process_args "$@"

cd $PROJECT_ROOT

echo "Installing clippy..."
$RUSTUP component add clippy

echo "Running clippy..."
$CARGO clippy --workspace --all-features
$CARGO clippy --workspace --tests --all-features
$CARGO clippy --workspace --examples
$CARGO clippy --workspace --benches
