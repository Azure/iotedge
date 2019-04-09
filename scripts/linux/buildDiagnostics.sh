#!/bin/bash

# This script builds the iotedge-diagnostics binary that goes into the azureiotedge-diagnostics image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes the binaries along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

BuildConfiguration=${1,,}
BuildConfiguration=${BuildConfiguration:-release}

set -euo pipefail

VERSIONINFO_FILE_PATH=$BUILD_REPOSITORY_LOCALPATH/versionInfo.json
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
export VERSION="$(cat "$VERSIONINFO_FILE_PATH" | jq '.version' -r)"

mkdir -p $PUBLISH_FOLDER/azureiotedge-diagnostics/
cp -R $BUILD_REPOSITORY_LOCALPATH/edgelet/iotedge-diagnostics/docker $PUBLISH_FOLDER/azureiotedge-diagnostics/docker

cd "$BUILD_REPOSITORY_LOCALPATH/edgelet"

BuildOption="--$BuildConfiguration"
cross build -p iotedge-diagnostics $BuildOption --target x86_64-unknown-linux-musl
strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/$BuildConfiguration/iotedge-diagnostics
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/$BuildConfiguration/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/amd64/

cross build -p iotedge-diagnostics $BuildOption --target armv7-unknown-linux-musleabihf
arm-linux-gnueabihf-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/$BuildConfiguration/iotedge-diagnostics
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/$BuildConfiguration/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm32v7/

cross build -p iotedge-diagnostics $BuildOption --target aarch64-unknown-linux-musl
aarch64-linux-gnu-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/aarch64-unknown-linux-musl/$BuildConfiguration/iotedge-diagnostics
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/aarch64-unknown-linux-musl/$BuildConfiguration/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm64v8/
