#!/bin/bash

# This script builds the iotedge-diagnostics binary that goes into the azureiotedge-diagnostics image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes the binaries along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

BUILD_CONFIGURATION=${1:-release}

if [[ "${BUILD_CONFIGURATION,,}" == 'release' ]]; then
    BUILD_CONFIGURATION='release'
    BUILD_CONFIG_OPTION='--release'
else
    BUILD_CONFIGURATION='debug'
    BUILD_CONFIG_OPTION=''
fi

set -euo pipefail

VERSIONINFO_FILE_PATH=$BUILD_REPOSITORY_LOCALPATH/versionInfo.json
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
export VERSION="$(cat "$VERSIONINFO_FILE_PATH" | jq '.version' -r)"

mkdir -p $PUBLISH_FOLDER/azureiotedge-diagnostics/
cp -R $BUILD_REPOSITORY_LOCALPATH/edgelet/iotedge-diagnostics/docker $PUBLISH_FOLDER/azureiotedge-diagnostics/docker

cd "$BUILD_REPOSITORY_LOCALPATH/edgelet"

cross build -p iotedge-diagnostics $BUILD_CONFIG_OPTION --target x86_64-unknown-linux-musl
strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedge-diagnostics
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/amd64/

cross build -p iotedge-diagnostics $BUILD_CONFIG_OPTION --target armv7-unknown-linux-musleabihf
arm-linux-gnueabihf-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/$BUILD_CONFIGURATION/iotedge-diagnostics
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/$BUILD_CONFIGURATION/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm32v7/

cross build -p iotedge-diagnostics $BUILD_CONFIG_OPTION --target aarch64-unknown-linux-musl
aarch64-linux-gnu-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/aarch64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedge-diagnostics
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/aarch64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm64v8/
