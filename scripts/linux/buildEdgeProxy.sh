#!/bin/bash

# This script copies the edge-proxy executable files that goes into the azureiotedge-proxy image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes executable files along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

VERSIONINFO_FILE_PATH=$BUILD_REPOSITORY_LOCALPATH/versionInfo.json
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
export VERSION="$(cat "$VERSIONINFO_FILE_PATH" | jq '.version' -r)"

mkdir -p $PUBLISH_FOLDER/azureiotedge-proxy/
cp -R $BUILD_REPOSITORY_LOCALPATH/edge-proxy/docker $PUBLISH_FOLDER/azureiotedge-proxy/docker

cp $BUILD_REPOSITORY_LOCALPATH/edge-proxy/src/run.sh $PUBLISH_FOLDER/azureiotedge-proxy/docker/linux/amd64/
cp $BUILD_REPOSITORY_LOCALPATH/edge-proxy/src/run.sh $PUBLISH_FOLDER/azureiotedge-proxy/docker/linux/arm32v7/
cp $BUILD_REPOSITORY_LOCALPATH/edge-proxy/src/run.sh $PUBLISH_FOLDER/azureiotedge-proxy/docker/linux/arm64v8/