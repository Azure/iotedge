#!/bin/bash

# This script copies the iodedged executable files that go into the azureiotedge-iotedged image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes executable files along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

set -e
###############################################################################
# Define Environment Variables
###############################################################################

ARCH=$1
DOCKER_REGISTRY=$2
DOCKER_USERNAME=$3
DOCKER_PASSWORD=$4
DOCKER_IMAGEVERSION=${BUILD_BUILDNUMBER:=$5}

if [[ "$ARCH" == "amd64" ]]; then
    ARCH="amd64"
    DOCKER_IMAGENAME="azureiotedge/mqttd-linux-amd64"
    TARGET="x86_64-unknown-linux-musl"
    DOCKERFILE="../../docker/linux/amd64/Dockerfile"
elif [[ "$ARCH" == "armv7" ]]; then
    ARCH="arm32v7"
    DOCKER_IMAGENAME="azureiotedge/mqttd-linux-arm32v7"
    TARGET="armv7-unknown-linux-musleabihf"
    DOCKERFILE="../../docker/linux/arm32v7/Dockerfile"
else
    echo "Unsupported architecture"
    exit 1
fi

echo "Arch:                 $ARCH"
echo "DOCKER_IMAGENAME:     $DOCKER_IMAGENAME"
echo "TARGET:               $TARGET"
echo "DOCKERFILE:           $DOCKERFILE"

echo "Building and pushing Docker image $imagename for $arch"
docker_build_cmd="docker build --no-cache"
docker_build_cmd+=" -t $DOCKER_REGISTRY/$DOCKER_IMAGENAME"
docker_build_cmd+=" --file $DOCKERFILE"
docker_build_cmd+=" ../.."

echo "Running... $docker_build_cmd"

${docker_build_cmd}

if [[ $? -ne 0 ]]; then
    echo "Docker build failed with exit code $?"
    exit 1
fi

echo "Done building Docker image $DOCKER_IMAGENAME for $PROJECT"
