#!/bin/bash

###############################################################################
# This script sets up the docker build context for various entities.

# When copying rust artifacts into the build context we try to mimic the
# directory structure of the target folder.

# For simplicity this script tries to move all artifact types regardless if
# they have been produced by a build step. This is helpful for local builds
# and the mqtt image build.
###############################################################################

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
DESTINATION_DIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}/publish
DOTNET_ARTIFACTS_SOURCE_DIRECTORY=${BUILD_BINARIESDIRECTORY}/publish
RUST_ARTIFACTS_SOURCE_DIRECTORY="./"
SCRIPT_NAME=$(basename "$0")

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " --artifact-name                  The name of the consolidated artifact directory."
    echo " --dest-dir                       The directory in which to place the consolidated artifacts."
    echo " --dotnet-artifacts-source-dir    The directory containing the built dotnet source code to be consolidated."   
    echo " --rust-artifacts-source-dir      The directory containing the built Rust source code to be consolidated."
    echo " -h, --help                       Print this help and exit."
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
            DESTINATION_DIRECTORY=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 3 ]; then
            DOTNET_ARTIFACTS_SOURCE_DIRECTORY=$arg
            save_next_arg=0       
        elif [ ${save_next_arg} -eq 4 ]; then
            RUST_ARTIFACTS_SOURCE_DIRECTORY=$arg
            save_next_arg=0                     
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "--artifact-name") save_next_arg=1 ;;
            "--dest-dir") save_next_arg=2 ;;
            "--dotnet-artifacts-source-dir") save_next_arg=3 ;;
            "--rust-artifacts-source-dir") save_next_arg=4 ;;
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
TARGET_AMD64_GNU="x86_64-unknown-linux-gnu"

case "$ARTIFACT_NAME" in
edge-hub)
    EDGEHUB_ARTIFACTS_SOURCE="$DOTNET_ARTIFACTS_SOURCE_DIRECTORY/Microsoft.Azure.Devices.Edge.Hub.Service"
    MQTT_ARTIFACTS_SOURCE="$RUST_ARTIFACTS_SOURCE_DIRECTORY/mqtt"
    WATCHDOG_ARTIFACTS_SOURCE="$RUST_ARTIFACTS_SOURCE_DIRECTORY/edge-hub/watchdog"
    EDGEHUB_DOCKER_SOURCE="edge-hub/docker"
    ARTIFACTS_DEST="${DESTINATION_DIRECTORY}/$ARTIFACT_NAME"

    # make build context structure
    mkdir -p "$ARTIFACTS_DEST"
    mkdir -p "$ARTIFACTS_DEST/mqtt"
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
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_AMD64_MUSL/release/mqttd" "$ARTIFACTS_DEST/mqtt/$TARGET_AMD64_MUSL/release" || true
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/mqttd" "$ARTIFACTS_DEST/mqtt/$TARGET_ARM32V7/release" || true
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/mqttd" "$ARTIFACTS_DEST/mqtt/$TARGET_ARM64V8/release" || true
    cp "mqtt/contrib/edgehub/broker.json" "$ARTIFACTS_DEST/mqtt" || true

    # copy watchdog artifacts
    cp "$WATCHDOG_ARTIFACTS_SOURCE/target/$TARGET_AMD64_MUSL/release/watchdog" "$ARTIFACTS_DEST/watchdog/$TARGET_AMD64_MUSL/release" || true
    cp "$WATCHDOG_ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/watchdog" "$ARTIFACTS_DEST/watchdog/$TARGET_ARM32V7/release" || true
    cp "$WATCHDOG_ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/watchdog" "$ARTIFACTS_DEST/watchdog/$TARGET_ARM64V8/release" || true
    cp -r "$EDGEHUB_DOCKER_SOURCE" "$ARTIFACTS_DEST"
    ;;

mqttd)
    MQTT_ARTIFACTS_SOURCE="mqtt"
    ARTIFACTS_DEST="mqtt/build-context"

    # make build context structure
    mkdir "$ARTIFACTS_DEST"
    mkdir -p "$ARTIFACTS_DEST/$TARGET_AMD64_MUSL/release"
    mkdir -p "$ARTIFACTS_DEST/$TARGET_ARM32V7/release"
    mkdir -p "$ARTIFACTS_DEST/$TARGET_ARM64V8/release"

    # copy mqtt artifacts
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_AMD64_MUSL/release/mqttd" "$ARTIFACTS_DEST/$TARGET_AMD64_MUSL/release" || true
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_ARM32V7/release/mqttd" "$ARTIFACTS_DEST/$TARGET_ARM32V7/release" || true
    cp "$MQTT_ARTIFACTS_SOURCE/target/$TARGET_ARM64V8/release/mqttd" "$ARTIFACTS_DEST/$TARGET_ARM64V8/release" || true

    # this script is used for the mqtt image pipeline, which doesn't need the docker folder in the artifact location because it uses the Docker@2 task
    ;;

*)
    print_error "Invalid artifact name"
    exit 1
    ;;
esac
