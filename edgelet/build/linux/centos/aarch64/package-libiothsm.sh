#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet

BUILD_DIR_REL="target/hsm/build/aarch64"
TRIPLET=aarch64-unknown-linux-gnu
TOOLCHAIN=aarch64-linux-gnu
BUILD_DIR="$PROJECT_ROOT/$BUILD_DIR_REL"
IMAGE="edgebuilds.azurecr.io/gcc-linaro-7.3.1-2018.05-x86_64_${TOOLCHAIN}:centos_7.5-1"

PACKAGE_NAME="libiothsm-std"
REVISION=${REVISION:-1}
DEFAULT_VERSION=$(cat $PROJECT_ROOT/version.txt)
VERSION="${VERSION:-$DEFAULT_VERSION}"

# Converts debian versioning to rpm version
# deb 1.0.1~dev100 ~> rpm 1.0.1-0.1.dev100

RPM_VERSION=`echo "$VERSION" | cut -d"~" -f1`
RPM_TAG=`echo "$VERSION" | cut -s -d"~" -f2`
if [[ ! -z ${RPM_TAG} ]]; then
    RPM_RELEASE="0.${REVISION}.${RPM_TAG}"
else
    RPM_RELEASE="${REVISION}"
fi

docker pull "$IMAGE"

run_command()
{
    echo "$1"
    docker \
      run \
      --rm \
      -e "USER=root" \
      -e "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/toolchain/${TOOLCHAIN}/bin:/cargo/bin" \
      -v "$PROJECT_ROOT/target:/target" \
      -v "$PROJECT_ROOT:/project" \
      -i "$IMAGE" \
      sh -c "$1"
}

mkdir -p $BUILD_DIR
run_command "cd /$BUILD_DIR_REL && cmake -DCMAKE_SYSROOT=/toolchain//${TOOLCHAIN}/libc -DCMAKE_C_COMPILER=/toolchain/bin//${TOOLCHAIN}-gcc -DCMAKE_CXX_COMPILER=/toolchain/bin//${TOOLCHAIN}-g++ -DCMAKE_SYSTEM_NAME=Linux -DCMAKE_SYSTEM_VERSION=1 -DCPACK_RPM_PACKAGE_ARCHITECTURE=aarch64 -DCPACK_PACKAGE_VERSION=\"$RPM_VERSION\" -DCPACK_RPM_PACKAGE_RELEASE=\"$RPM_RELEASE\" -DBUILD_SHARED=On -Drun_unittests=Off -Duse_emulator=Off -DCMAKE_BUILD_TYPE=Release -Duse_default_uuid=On -Duse_http=Off -DCPACK_GENERATOR=RPM /project/hsm-sys/azure-iot-hsm-c/"

run_command "cd /$BUILD_DIR_REL && make package"
