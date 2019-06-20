#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet

BUILD_DIR=$PROJECT_ROOT/target/hsm/build
CARGO_HOME="$HOME/.cargo/"
RUSTUP_HOME="$HOME/.rustup"
IMAGE="edgebuilds.azurecr.io/gcc-linaro-7.4.1-2019.02-x86_64_aarch64-linux-gnu:ubuntu18.04-1"

PACKAGE_NAME="libiothsm-std"
REVISION=${REVISION:-1}
DEFAULT_VERSION=$(cat $PROJECT_ROOT/version.txt)
VERSION="${VERSION:-$DEFAULT_VERSION}-$REVISION"

docker pull "$IMAGE"

run_command()
{
    echo "$1"
    docker \
      run \
      --rm \
      -e "USER=root" \
      -e "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/toolchain/aarch64-linux-gnu/bin" \
      -v "$PROJECT_ROOT/target:/target" \
      -v "$PROJECT_ROOT:/project" \
      -i "$IMAGE" \
      sh -c "$1"
}

mkdir -p $BUILD_DIR
run_command "cd /target/hsm/build && cmake -DCMAKE_SYSROOT=/toolchain/aarch64-linux-gnu/libc -DCMAKE_C_COMPILER=/toolchain/bin/aarch64-linux-gnu-gcc -DCMAKE_CXX_COMPILER=/toolchain/bin/aarch64-linux-gnu-g++ -DCMAKE_SYSTEM_NAME=Linux -DCMAKE_SYSTEM_VERSION=1 -DCPACK_DEBIAN_PACKAGE_ARCHITECTURE=arm64 -DCPACK_PACKAGE_VERSION=\"$VERSION\" -DBUILD_SHARED=On -Drun_unittests=On -Duse_emulator=Off -Duse_http=Off -DCMAKE_BUILD_TYPE=Release -Duse_default_uuid=On /project/hsm-sys/azure-iot-hsm-c/"

run_command "cd /target/hsm/build && make package"

# uncomment if we revert to an older CPACK, old CPACK produces non-standard deb package filenames.
# This renames them
#for f in $BUILD_DIR/$PACKAGE_NAME-*-Linux.deb ; do
#    echo "Renaming package $(basename "$f") to $(basename "$BUILD_DIR/${PACKAGE_NAME}_${VERSION}_armhf.deb")"
#    mv -f "$f" "$BUILD_DIR/${PACKAGE_NAME}_${VERSION}_armhf.deb"
#done
