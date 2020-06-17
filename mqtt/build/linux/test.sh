#!/bin/bash

###############################################################################
# This script tests the project
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
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"
TARGET="x86_64-unknown-linux-gnu"
RELEASE=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help          Print this help and exit."
    echo " -r, --release       Release build? (flag, default: false)"
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
            TARGET="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            RELEASE="true"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            CARGO="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target" ) save_next_arg=1;;
                "-r" | "--release" ) save_next_arg=2;;
                "-c" | "--cargo" ) save_next_arg=3;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

if [[ -z ${RELEASE} ]]; then
    cd "$PROJECT_ROOT" && $CARGO test --workspace --all-features --target "$TARGET"
else
    cd "$PROJECT_ROOT" && $CARGO test --workspace --all-features --release --target "$TARGET"
fi
