#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
BUILD_REPOSITORY_LOCALPATH="${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../..}"
PROJECT_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
IOTEDGED_DIR="$PROJECT_ROOT/iotedged"

BUILD_DIR="$PROJECT_ROOT/target/hsm/build-kubernetes"
DOCKER_DIR="$BUILD_DIR/docker"
CARGO_HOME="$HOME/.cargo/"

# Build the docker variables
IMAGE_BASE_NAME="edgebuilds.azurecr.io/microsoft/azureiotedge-iotedged"
DEFAULT_VERSION=$(awk -F '~' -- '{ print $1 }' "$PROJECT_ROOT/version.txt")
IMAGE_NAME="$IMAGE_BASE_NAME:$DEFAULT_VERSION-linux-amd64"
DOCKER_IMAGE_FILE="$PROJECT_ROOT/build/linux/kubernetes/amd64/Dockerfile"

# Build libiothsm.so
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"
cmake \
    -DBUILD_SHARED=ON \
    -Drun_unittests=OFF \
    -Duse_emulator=OFF \
    -Duse_http=OFF \
    -DCMAKE_BUILD_TYPE=Release \
    -Duse_default_uuid=ON \
    "$PROJECT_ROOT/hsm-sys/azure-iot-hsm-c"
make -j "$(nproc)"

# Build iotedged
cd "$IOTEDGED_DIR"
"$CARGO_HOME/bin/cargo" build --no-default-features --features runtime-kubernetes --release

# Prepare folder to build docker image
mkdir -p "$DOCKER_DIR"
cp "$BUILD_DIR/libiothsm.so.1" "$DOCKER_DIR"
cp "$PROJECT_ROOT/target/release/iotedged" "$DOCKER_DIR"

# Build docker image
echo "Building image: $IMAGE_NAME"
docker build -t "$IMAGE_NAME" -f "$DOCKER_IMAGE_FILE" "$DOCKER_DIR"