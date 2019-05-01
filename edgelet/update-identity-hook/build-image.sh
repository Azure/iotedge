#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
BUILD_REPOSITORY_LOCALPATH="${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}"
PROJECT_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
UPDATE_IDENTITY_HOOK_DIR="$PROJECT_ROOT/update-identity-hook"
BUILD_CONFIG="release"

BUILD_DIR="$PROJECT_ROOT/target/$BUILD_CONFIG"
DOCKER_DIR="$BUILD_DIR/docker/update-identity-hook"
CARGO_HOME="$HOME/.cargo/"

# Build the docker variables
IMAGE_BASE_NAME="edgebuilds.azurecr.io/microsoft/update-identity-hook"
DEFAULT_VERSION=$(awk -F '~' -- '{ print $1 }' "$PROJECT_ROOT/version.txt")
IMAGE_NAME="$IMAGE_BASE_NAME:$DEFAULT_VERSION-linux-amd64"
DOCKER_IMAGE_FILE="$UPDATE_IDENTITY_HOOK_DIR/docker/linux/amd64/Dockerfile"

# Build update-identity-hook
cd "$UPDATE_IDENTITY_HOOK_DIR"
if [ "$BUILD_CONFIG" = "release" ]; then
  "$CARGO_HOME/bin/cargo" build --release
else
  "$CARGO_HOME/bin/cargo" build
fi

# Prepare folder to build docker image
mkdir -p "$DOCKER_DIR"
cp "$PROJECT_ROOT/target/$BUILD_CONFIG/update-identity-hook" "$DOCKER_DIR"

# Build docker image
echo "Building image: $IMAGE_NAME"
docker build -t "$IMAGE_NAME" -f "$DOCKER_IMAGE_FILE" "$DOCKER_DIR"