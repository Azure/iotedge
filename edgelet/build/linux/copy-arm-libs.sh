#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

IMAGE_LIB_PATH=/arm-linux-gnueabihf/libc/usr/lib/
LIB_VERSION=1.0.2
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet
DEST_DIR=$PROJECT_ROOT/target
IMAGE="edgebuilds.azurecr.io/gcc-linaro-7.2.1-2017.11-x86_64_arm-linux-gnueabihf:0.2"

docker pull "$IMAGE"

run_command()
{
    echo "$1"
    docker \
      run \
      --rm \
      -e "USER=root" \
      -e "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/arm-linux-gnueabihf/bin" \
      -v "$PROJECT_ROOT/target:/target" \
      -v "$PROJECT_ROOT:/project" \
      -i "$IMAGE" \
      sh -c "$1"
}

mkdir -p $DEST_DIR

run_command "cp $IMAGE_LIB_PATH/libssl.so.${LIB_VERSION} $IMAGE_LIB_PATH/libcrypto.so.${LIB_VERSION} /target" 

pushd $DEST_DIR
sudo cp -n libssl.so.${LIB_VERSION} libcrypto.so.${LIB_VERSION} /usr/arm-linux-gnueabihf/lib/
