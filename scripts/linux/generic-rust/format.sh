#!/bin/bash

###############################################################################
# This script runs cargo fmt on your project
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

SCRIPT_NAME=$(basename "$0")
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}
RUSTUP="${CARGO_HOME:-"$HOME/.cargo"}/bin/rustup"
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help          Print this help and exit."
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ ${save_next_arg} -eq 1 ]; then
            PROJECT_ROOT=${PROJECT_ROOT}/$arg
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "--project-root" ) save_next_arg=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

cd $PROJECT_ROOT

echo "Installing rustfmt"
$RUSTUP component add rustfmt

echo "Running cargo fmt"
$CARGO fmt --all -- --check
