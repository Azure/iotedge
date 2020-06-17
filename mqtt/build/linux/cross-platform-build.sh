#!/bin/bash

# TODO: remove amd64 steps as they are not for alpine

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"

REVISION="${REVISION:-1}"
DEFAULT_VERSION="1.0.0"
VERSION="${VERSION:-$DEFAULT_VERSION}"

CMAKE_ARGS='-DCMAKE_BUILD_TYPE=Release'
CMAKE_ARGS="$CMAKE_ARGS -DBUILD_SHARED=On -Drun_unittests=Off -Duse_default_uuid=On -Duse_emulator=Off -Duse_http=Off"

DOCKER_VOLUME_MOUNTS=''

PACKAGE_OS='ubuntu16.04'
PACKAGE_ARCH='aarch64'

case "$PACKAGE_OS" in
    'ubuntu16.04')
        DOCKER_IMAGE='ubuntu:16.04'

        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=DEB"
        # TODO: Do we need?
        # The cmake in this image doesn't understand CPACK_DEBIAN_PACKAGE_RELEASE, so include the REVISION in CPACK_PACKAGE_VERSION
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$VERSION-$REVISION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DOPENSSL_DEPENDS_SPEC=libssl1.0.0'"
        ;;

    'ubuntu18.04')
        DOCKER_IMAGE='ubuntu:18.04'

        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=DEB"
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$VERSION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_DEBIAN_PACKAGE_RELEASE=$REVISION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DOPENSSL_DEPENDS_SPEC=libssl1.1'"
        ;;
esac

if [ -z "$DOCKER_IMAGE" ]; then
    echo "Unrecognized target [$PACKAGE_OS.$PACKAGE_ARCH]" >&2
    exit 1
fi

case "$PACKAGE_ARCH" in
    'amd64')
        MAKE_FLAGS="DPKGFLAGS='-b -us -uc -i'"
        ;;

    'arm32v7')
        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_DEBIAN_PACKAGE_ARCHITECTURE=armhf"
        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_RPM_PACKAGE_ARCHITECTURE=armv7hl"

        RUST_TARGET='armv7-unknown-linux-gnueabihf'
        ;;

    'aarch64')
        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_DEBIAN_PACKAGE_ARCHITECTURE=arm64"
        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_RPM_PACKAGE_ARCHITECTURE=aarch64"

        RUST_TARGET='aarch64-unknown-linux-gnu'
        ;;
esac

if [ -n "$RUST_TARGET" ]; then
    RUST_TARGET_COMMAND="rustup target add $RUST_TARGET &&"
fi


case "$PACKAGE_OS.$PACKAGE_ARCH" in
    ubuntu16.04.amd64|ubuntu18.04.amd64)
        SETUP_COMMAND=$'
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git make \
                gcc g++ pkg-config \
                libcurl4-openssl-dev libssl-dev uuid-dev &&
        '
        ;;

    ubuntu16.04.arm32v7|ubuntu18.04.arm32v7)
        SETUP_COMMAND=$'
            sources="$(cat /etc/apt/sources.list | grep -E \'^[^#]\')" &&
            # Update existing repos to be specifically for amd64
            echo "$sources" | sed -e \'s/^deb /deb [arch=amd64] /g\' > /etc/apt/sources.list &&
            # Add armhf repos
            echo "$sources" |
                sed -e \'s/^deb /deb [arch=armhf] /g\' \
                    -e \'s| http://archive.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    -e \'s| http://security.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    >> /etc/apt/sources.list &&
        '
        case "$PACKAGE_OS" in
            ubuntu16.04)
                SETUP_COMMAND="
                    $SETUP_COMMAND

                    # Add 14.04 repos because 16.04\'s libc6-dev:armhf cannot coexist with libc6-dev
                    echo 'deb [arch=amd64] http://archive.ubuntu.com/ubuntu/ trusty main universe' > /etc/apt/sources.list.d/trusty.list &&
                    echo 'deb [arch=armhf] http://ports.ubuntu.com/ubuntu-ports/ trusty main universe' >> /etc/apt/sources.list.d/trusty.list &&
                "
                ;;
        esac
        SETUP_COMMAND="
            $SETUP_COMMAND

            dpkg --add-architecture armhf &&
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git make \
                gcc g++ \
                gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf \
                libcurl4-openssl-dev:armhf libssl-dev:armhf uuid-dev:armhf &&

            mkdir -p ~/.cargo &&
            echo '[target.armv7-unknown-linux-gnueabihf]' > ~/.cargo/config &&
            echo 'linker = \"arm-linux-gnueabihf-gcc\"' >> ~/.cargo/config &&
            export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_LIB_DIR=/usr/lib/arm-linux-gnueabihf &&
            export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_INCLUDE_DIR=/usr/include &&
        "

        # Indicate to cmake that we're cross-compiling
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_SYSTEM_NAME=Linux -DCMAKE_SYSTEM_VERSION=1"

        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=arm-linux-gnueabihf-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=arm-linux-gnueabihf-g++"
        ;;

    ubuntu16.04.aarch64|ubuntu18.04.aarch64)
        SETUP_COMMAND=$'
            sources="$(cat /etc/apt/sources.list | grep -E \'^[^#]\')" &&
            # Update existing repos to be specifically for amd64
            echo "$sources" | sed -e \'s/^deb /deb [arch=amd64] /g\' > /etc/apt/sources.list &&
            # Add arm64 repos
            echo "$sources" |
                sed -e \'s/^deb /deb [arch=arm64] /g\' \
                    -e \'s| http://archive.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    -e \'s| http://security.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    >> /etc/apt/sources.list &&
        '
        case "$PACKAGE_OS" in
            ubuntu16.04)
                SETUP_COMMAND="
                    $SETUP_COMMAND

                    # Add 14.04 repos because 16.04\'s libc6-dev:arm64 cannot coexist with libc6-dev
                    echo 'deb [arch=amd64] http://archive.ubuntu.com/ubuntu/ trusty main universe' > /etc/apt/sources.list.d/trusty.list &&
                    echo 'deb [arch=arm64] http://ports.ubuntu.com/ubuntu-ports/ trusty main universe' >> /etc/apt/sources.list.d/trusty.list &&
                "
                ;;
        esac
        SETUP_COMMAND="
            $SETUP_COMMAND

            dpkg --add-architecture arm64 &&
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git make \
                gcc g++ \
                gcc-aarch64-linux-gnu g++-aarch64-linux-gnu \
                libcurl4-openssl-dev:arm64 libssl-dev:arm64 uuid-dev:arm64 &&

            mkdir -p ~/.cargo &&
            echo '[target.aarch64-unknown-linux-gnu]' > ~/.cargo/config &&
            echo 'linker = \"aarch64-linux-gnu-gcc\"' >> ~/.cargo/config &&
            export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_LIB_DIR=/usr/lib/aarch64-linux-gnu &&
            export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_INCLUDE_DIR=/usr/include &&
        "

        # Indicate to cmake that we're cross-compiling
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_SYSTEM_NAME=Linux -DCMAKE_SYSTEM_VERSION=1"

        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=aarch64-linux-gnu-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=aarch64-linux-gnu-g++"
        ;;
esac

if [ -z "$SETUP_COMMAND" ]; then
    echo "Unrecognized target [$PACKAGE_OS.$PACKAGE_ARCH]" >&2
    exit 1
fi

case "$PACKAGE_OS" in
    *)
        case "$PACKAGE_ARCH" in
            amd64)
                ;;
            arm32v7)
                MAKE_FLAGS="'CARGOFLAGS=--target armv7-unknown-linux-gnueabihf'"
                MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/armv7-unknown-linux-gnueabihf/release'"
                MAKE_FLAGS="$MAKE_FLAGS 'DPKGFLAGS=-b -us -uc -i --host-arch armhf'"
                ;;
            aarch64)
                MAKE_FLAGS="'CARGOFLAGS=--target aarch64-unknown-linux-gnu'"
                MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/aarch64-unknown-linux-gnu/release'"
                MAKE_FLAGS="$MAKE_FLAGS 'DPKGFLAGS=-b -us -uc -i --host-arch arm64 --host-type aarch64-linux-gnu --target-type aarch64-linux-gnu'"
                ;;
        esac

        MAKE_COMMAND="make release 'VERSION=$VERSION' 'REVISION=$REVISION' $MAKE_FLAGS"
        ;;
esac

docker run --rm \
    --user root \
    -e 'USER=root' \
    -v "$BUILD_REPOSITORY_LOCALPATH:/project" \
    -i \
    $DOCKER_VOLUME_MOUNTS \
    "$DOCKER_IMAGE" \
    sh -c "
        set -e &&

        cat /etc/os-release &&

        $SETUP_COMMAND

        echo 'Installing rustup' &&
        curl -sSLf https://sh.rustup.rs | sh -s -- -y &&
        . ~/.cargo/env &&

        # mqttd
        cd /project/mqtt/mqttd &&
        $RUST_TARGET_COMMAND
        $MAKE_COMMAND
    "
