#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet

BUILD_DIR=$PROJECT_ROOT/target/hsm/build
CARGO_HOME="$HOME/.cargo/"
RUSTUP_HOME="$HOME/.rustup"
IMAGE="edgebuilds.azurecr.io/gcc-linaro-7.2.1-2017.11-x86_64_arm-linux-gnueabihf:0.1"

PACKAGE_NAME="libiothsm-std"
REVISION=${REVISION:-1}
VERSION=$(cat $PROJECT_ROOT/version.txt)
DEBIAN_VERSION="$VERSION-$REVISION"

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

mkdir -p $BUILD_DIR
run_command "cd /target/hsm/build && cmake -DCMAKE_SYSROOT=/arm-linux-gnueabihf/libc -DCMAKE_C_COMPILER=/bin/arm-linux-gnueabihf-gcc -DCMAKE_CXX_COMPILER=/bin/arm-linux-gnueabihf-g++ -DCMAKE_SYSTEM_NAME=Linux -DCPACK_DEBIAN_PACKAGE_ARCHITECTURE=armhf -DCPACK_DEBIAN_PACKAGE_VERSION=\"$DEBIAN_VERSION\" -DBUILD_SHARED=On -Duse_emulator=Off -DCMAKE_BUILD_TYPE=Release -Duse_default_uuid=On /project/hsm-sys/azure-iot-hsm-c/"

run_command "cd /target/hsm/build && make package"

# Old CPACK produces non-standard deb package filenames.
# This renames them
echo "Renaming package"
mv "$BUILD_DIR/$PACKAGE_NAME-$VERSION.1-Linux.deb" "$BUILD_DIR/${PACKAGE_NAME}_${DEBIAN_VERSION}_armhf.deb"
