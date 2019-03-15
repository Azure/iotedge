#!/bin/bash

# This script builds the iotedge-diagnostics binary that goes into the azureiotedge-diagnostics image,
# for each supported arch (x86_64, arm32v7).
# It then publishes the binaries along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

set -euo pipefail

VERSIONINFO_FILE_PATH=$BUILD_REPOSITORY_LOCALPATH/versionInfo.json
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
export VERSION="$(cat "$VERSIONINFO_FILE_PATH" | jq '.version' -r)"

mkdir -p $PUBLISH_FOLDER/azureiotedge-diagnostics/
cp -R $BUILD_REPOSITORY_LOCALPATH/edgelet/iotedge-diagnostics/docker $PUBLISH_FOLDER/azureiotedge-diagnostics/docker

cd "$BUILD_REPOSITORY_LOCALPATH/edgelet"

cross build -p iotedge-diagnostics --release --target x86_64-unknown-linux-musl
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/release/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/amd64/

cross build -p iotedge-diagnostics --release --target armv7-unknown-linux-musleabihf
cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/release/iotedge-diagnostics $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm32v7/
