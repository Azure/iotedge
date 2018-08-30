#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet

BUILD_DIR_REL="target/hsm/build/amd64"
BUILD_DIR="$PROJECT_ROOT/$BUILD_DIR_REL"
IMAGE="edgebuilds.azurecr.io/debian-build:8.11-1"

PACKAGE_NAME="libiothsm-std"
REVISION=${REVISION:-1}
DEFAULT_VERSION=$(cat $PROJECT_ROOT/version.txt)
VERSION="${VERSION:-$DEFAULT_VERSION}"


docker pull "$IMAGE"

run_command()
{
    echo "$1"
    docker \
      run \
      --rm \
      -e "USER=root" \
      -e "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/cargo/bin" \
      -v "$PROJECT_ROOT/target:/target" \
      -v "$PROJECT_ROOT:/project" \
      -i "$IMAGE" \
      sh -c "$1"
}

mkdir -p $BUILD_DIR
run_command "cd /target/hsm/build && cmake DCMAKE_SYSTEM_NAME=Linux -DCPACK_DEBIAN_PACKAGE_ARCHITECTURE=amd64 -DCPACK_PACKAGE_VERSION=\"$VERSION\" -DBUILD_SHARED=On -Drun_unittests=On -Duse_emulator=Off -DCMAKE_BUILD_TYPE=Release -Duse_default_uuid=On -Duse_http=Off /project/hsm-sys/azure-iot-hsm-c/"

run_command "cd /target/hsm/build && make package"
