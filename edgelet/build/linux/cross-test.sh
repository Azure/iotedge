#!/bin/bash

###############################################################################
# This script builds the project
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
TARGET="armv7-unknown-linux-gnueabihf"
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
    echo " -t, --target        Target architecture"
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
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target" ) save_next_arg=1;;
                "-r" | "--release" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

if [[ -z ${RELEASE} ]]; then
    cd "$PROJECT_ROOT" && IOTEDGE_HOMEDIR=/tmp cross test --all --target "$TARGET"
else
    cd "$PROJECT_ROOT" && IOTEDGE_HOMEDIR=/tmp cross test --all --release --target "$TARGET"
fi
