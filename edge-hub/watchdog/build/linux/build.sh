#!/bin/bash

###############################################################################
# This script builds the project
###############################################################################

set -e

###############################################################################
# These are the packages this script will build.
###############################################################################
packages=(watchdog)

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../..}
PROJECT_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edge-hub/watchdog/linux"
SCRIPT_NAME=$(basename "$0")
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"

PACKAGES=
for p in "${packages[@]}"
do
    PACKAGES="${PACKAGES} -p ${p}"
done

echo "Release build: $RELEASE"

# TODO: If we move mqtt folder into edge hub folder then we can consolidate this build logic with the broker
EDGE_HUB_ARTIFACTS_PATH="target/publish/Microsoft.Azure.Devices.Edge.Hub.Service"
WATCHDOG_MANIFEST_PATH="${BUILD_REPOSITORY_LOCALPATH}/edge-hub/watchdog/linux/Cargo.toml"
LOCAL_TARGET_DIR_PATH="${PROJECT_ROOT}/target"
TARGET_DIR_PATH="${BUILD_REPOSITORY_LOCALPATH}/${EDGE_HUB_ARTIFACTS_PATH}/watchdog"
mkdir -p "${TARGET_DIR_PATH}"

echo "Building artifacts to ${TARGET_DIR_PATH}"

cd "$PROJECT_ROOT"

# Build for linux amd64
BUILD_COMMAND="$CARGO build ${PACKAGES} --release --manifest-path ${WATCHDOG_MANIFEST_PATH}"
echo "Building for amd64"
echo "${BUILD_COMMAND}"
eval "${BUILD_COMMAND}"
OUTPUT_BINARY="${LOCAL_TARGET_DIR_PATH}/release/watchdog"
strip "${OUTPUT_BINARY}"
cp ${OUTPUT_BINARY} ${TARGET_DIR_PATH} # needed because target-dir won't work with cross

# Build for linux arm32 and linux arm64
TARGET_PLATFORMS=("armv7-unknown-linux-gnueabihf" "aarch64-unknown-linux-gnu")
for platform in "${TARGET_PLATFORMS[@]}"
do
    BUILD_COMMAND_WITH_PLATFORM="${BUILD_COMMAND} --target $platform"

    echo "Building for $platform"
    echo "${BUILD_COMMAND_WITH_PLATFORM}"
    eval "${BUILD_COMMAND_WITH_PLATFORM}"

    OUTPUT_BINARY="${LOCAL_TARGET_DIR_PATH}/${platform}/release/watchdog"
    strip ${OUTPUT_BINARY}
    mkdir ${TARGET_DIR_PATH}/$platform && cp ${OUTPUT_BINARY} ${TARGET_DIR_PATH}/$platform # needed because target-dir won't work with cross
done
