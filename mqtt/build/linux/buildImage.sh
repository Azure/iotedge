#!/bin/bash

set -e

ARCH=$1
DOCKER_REGISTRY=$2
DOCKER_IMAGEVERSION=${BUILD_BUILDNUMBER:=$3}

if [[ "$ARCH" == "amd64" ]]; then
    ARCH="amd64"
    DOCKER_IMAGENAME="azureiotedge/mqttd-linux-amd64"
    DOCKERFILE="../../docker/linux/amd64/Dockerfile"
elif [[ "$ARCH" == "armv7" ]]; then
    ARCH="arm32v7"
    DOCKER_IMAGENAME="azureiotedge/mqttd-linux-arm32v7"
    DOCKERFILE="../../docker/linux/arm32v7/Dockerfile"
else
    echo "Unsupported architecture"
    exit 1
fi

echo "ARCH:                 $ARCH"
echo "DOCKER_IMAGENAME:     $DOCKER_IMAGENAME"
echo "TARGET:               $TARGET"
echo "DOCKERFILE:           $DOCKERFILE"
echo "DOCKER_IMAGEVERSION:  $DOCKER_IMAGEVERSION"

echo "Building and pushing Docker image $DOCKER_IMAGENAME for $ARCH"
docker_build_cmd="docker build --no-cache"
docker_build_cmd+=" -t $DOCKER_REGISTRY/$DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION"
docker_build_cmd+=" --file $DOCKERFILE"
docker_build_cmd+=" ../.."

echo "Running... $docker_build_cmd"

${docker_build_cmd}

if [[ $? -ne 0 ]]; then
    echo "Docker build failed with exit code $?"
    exit 1
fi

echo "Done building Docker image $DOCKER_IMAGENAME for $PROJECT"
