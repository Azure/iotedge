#!/bin/bash

###############################################################################
# This script sets up the docker build context. 
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}
SCRIPT_NAME=$(basename "$0")

TARGET_ARM32V7="armv7-unknown-linux-gnueabihf"
TARGET_ARM64V8="aarch64-unknown-linux-gnu"
TARGET_AMD64_MUSL="x86_64-unknown-linux-musl"

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " --artifact-name          The name of the artifact to consolidate for which to consolidate the artifacts."
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
    save_next_arg=0
    for arg in "$@"
    do
        if [ ${save_next_arg} -eq 1 ]; then
            ARTIFACT_NAME=$arg
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "--artifact-name" ) save_next_arg=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

cd $BUILD_REPOSITORY_LOCALPATH

if [ "$ARTIFACT_NAME" = "generic-mqtt-tester" ]; then
    ARTIFACTS_SOURCE="test/modules/generic-mqtt-tester"
    ARTIFACTS_DEST="${BUILD_BINARIESDIRECTORY}/publish/$ARTIFACT_NAME"

    mkdir "$ARTIFACTS_DEST"
    mkdir "$ARTIFACTS_DEST/amd64"
    mkdir "$ARTIFACTS_DEST/arm32v7"
    mkdir "$ARTIFACTS_DEST/arm64v8"

    cp "$ARTIFACTS_SOURCE/target/release/$ARTIFACT_NAME" "$ARTIFACTS_DEST/amd64"
    cp "$ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/$ARTIFACT_NAME" "$ARTIFACTS_DEST/arm32v7"
    cp "$ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/$ARTIFACT_NAME" "$ARTIFACTS_DEST/arm64v8"
fi