#!/bin/bash

###############################################################################
# This script sets up the docker build context for various entities.

# When copying rust artifacts into the build context we try to mimic the
# directory structure of the target folder.
###############################################################################

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}
SCRIPT_NAME=$(basename "$0")

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " --artifact-name           The name of the artifact to consolidate for which to consolidate the artifacts."
    echo " --build-binaries-dir      The name of the artifact to consolidate for which to consolidate the artifacts."
    echo " -h, --help                Print this help and exit."
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
            ARTIFACT_NAME=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 2 ]; then
            BUILD_BINARIESDIRECTORY=$arg
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--artifact-name") save_next_arg=1 ;;
            "--build-binaries-dir") save_next_arg=2 ;;
            *) usage ;;
            esac
        fi
    done
}

process_args "$@"

cd $BUILD_REPOSITORY_LOCALPATH

TARGET_ARM32V7="armv7-unknown-linux-gnueabihf"
TARGET_ARM64V8="aarch64-unknown-linux-gnu"
TARGET_AMD64_MUSL="x86_64-unknown-linux-musl"

case "$ARTIFACT_NAME" in
generic-mqtt-tester)
    ARTIFACTS_SOURCE="test/modules/$ARTIFACT_NAME"
    ARTIFACTS_DEST="${BUILD_BINARIESDIRECTORY}/publish/$ARTIFACT_NAME"

    # make build context structure
    mkdir "$ARTIFACTS_DEST"
    mkdir -p "$ARTIFACTS_DEST/release" #non-musl amd64 does not have arch specific folder
    mkdir -p "$ARTIFACTS_DEST/$TARGET_ARM32V7/release"
    mkdir -p "$ARTIFACTS_DEST/$TARGET_ARM64V8/release"

    # copy artifacts
    cp "$ARTIFACTS_SOURCE/target/release/$ARTIFACT_NAME" "$ARTIFACTS_DEST/release"
    cp "$ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/$ARTIFACT_NAME" "$ARTIFACTS_DEST/$TARGET_ARM32V7/release"
    cp "$ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/$ARTIFACT_NAME" "$ARTIFACTS_DEST/$TARGET_ARM64V8/release"
    cp -r "$ARTIFACTS_SOURCE/docker" "$ARTIFACTS_DEST"
    ;;

edge-hub)
    EDGEHUB_ARTIFACTS_SOURCE="$BUILD_BINARIESDIRECTORY/publish/Microsoft.Azure.Devices.Edge.Hub.Service"
    MQTT_ARTIFACTS_SOURCE="mqtt"
    WATCHDOG_ARTIFACTS_SOURCE="edge-hub/watchdog"
    EDGEHUB_DOCKER_SOURCE="edge-hub/docker"
    ARTIFACTS_DEST="${BUILD_BINARIESDIRECTORY}/publish/$ARTIFACT_NAME"

    # make build context structure
    mkdir "$ARTIFACTS_DEST"
    mkdir "$ARTIFACTS_DEST/mqtt"
    mkdir -p "$ARTIFACTS_DEST/mqtt/$TARGET_AMD64_MUSL/release"
    mkdir -p "$ARTIFACTS_DEST/mqtt/$TARGET_ARM32V7/release"
    mkdir -p "$ARTIFACTS_DEST/mqtt/$TARGET_ARM64V8/release"
    mkdir "$ARTIFACTS_DEST/watchdog"
    mkdir -p "$ARTIFACTS_DEST/watchdog/$TARGET_AMD64_MUSL/release"
    mkdir -p "$ARTIFACTS_DEST/watchdog/$TARGET_ARM32V7/release"
    mkdir -p "$ARTIFACTS_DEST/watchdog/$TARGET_ARM64V8/release"

    # copy edgehub core artifacts
    cp -r "$EDGEHUB_ARTIFACTS_SOURCE" "$ARTIFACTS_DEST"

    # copy mqtt artifacts
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_AMD64_MUSL/release/mqttd" "$ARTIFACTS_DEST/mqtt/$TARGET_AMD64_MUSL/release"
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/mqttd" "$ARTIFACTS_DEST/mqtt/$TARGET_ARM32V7/release"
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/mqttd" "$ARTIFACTS_DEST/mqtt/$TARGET_ARM64V8/release"
    cp "$MQTT_ARTIFACTS_SOURCE/contrib/edgehub/broker.json" "$ARTIFACTS_DEST/mqtt"

    # copy watchdog artifacts
    cp "$WATCHDOG_ARTIFACTS_SOURCE/target/$TARGET_AMD64_MUSL/release/watchdog" "$ARTIFACTS_DEST/watchdog/$TARGET_AMD64_MUSL/release"
    cp "$WATCHDOG_ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/watchdog" "$ARTIFACTS_DEST/watchdog/$TARGET_ARM32V7/release"
    cp "$WATCHDOG_ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/watchdog" "$ARTIFACTS_DEST/watchdog/$TARGET_ARM64V8/release"
    cp -r "$EDGEHUB_DOCKER_SOURCE" "$ARTIFACTS_DEST"
    ;;

*)
    print_error "Invalid artifact name"
    exit 1
    ;;
esac
