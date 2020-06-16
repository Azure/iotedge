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
PROJECT_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edge-hub/watchdog"
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
TARGET_DIR_PATH="${BUILD_REPOSITORY_LOCALPATH}/${EDGE_HUB_ARTIFACTS_PATH}/watchdog"

echo "Building artifacts to ${TARGET_DIR_PATH}"

cd "$PROJECT_ROOT"

# Build for linux amd64
BUILD_COMMAND="$CARGO build ${PACKAGES} --release --manifest-path ${WATCHDOG_MANIFEST_PATH} --target-dir ${TARGET_DIR_PATH}"
echo "${BUILD_COMMAND}"
eval "${BUILD_COMMAND}"
strip "${TARGET_DIR_PATH}/release/watchdog"

# Build for linux arm32 and linux arm64
TARGET_PLATFORMS=("armv7-unknown-linux-gnueabihf" "aarch64-unknown-linux-gnu")
for platform in "${TARGET_PLATFORMS[@]}"
do
    TMP_BUILD_COMMAND="${BUILD_COMMAND} --target $platform"

    echo "Building for $platform"
    echo "${TMP_BUILD_COMMAND}"

    eval "${TMP_BUILD_COMMAND}"
    strip "${TARGET_DIR_PATH}/${platform}/release/watchdog"
done
