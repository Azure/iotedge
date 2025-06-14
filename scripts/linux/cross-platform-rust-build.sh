#!/bin/bash

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "This script is used for building a few rust components (e.g., api proxy module)."
    echo "The logic here is similar to the edgelet packaging."
    echo ""
    echo "options"
    echo " --os                      Desired os for build"
    echo " --arch                    Desired arch for build"
    echo " --build-path              Desired path for build"
    echo " --cargo-flags             Flags for compilation"
    echo " -h, --help                Print this help and exit."
    exit 1;
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            PACKAGE_OS="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            PACKAGE_ARCH="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            BUILD_PATH="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            CARGOFLAGS="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--os" ) save_next_arg=1;;
                "--arch" ) save_next_arg=2;;
                "--build-path" ) save_next_arg=3;;
                "--cargo-flags" ) save_next_arg=4;;
                "-h" | "--help" ) usage;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

[[ -z "$PACKAGE_OS" ]] && { print_error 'OS is a required parameter'; exit 1; }
[[ -z "$PACKAGE_ARCH" ]] && { print_error 'Arch is a required parameter'; exit 1; }
[[ -z "$BUILD_PATH" ]] && { print_error 'Build path is a required parameter'; exit 1; }

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}")"

DOCKER_VOLUME_MOUNTS=''

case "$PACKAGE_OS" in
    'debian12')
        DOCKER_IMAGE='mcr.microsoft.com/mirror/docker/library/debian:bookworm-slim'
        ;;

    'alpine')
        DOCKER_IMAGE='mcr.microsoft.com/mirror/docker/library/ubuntu:22.04'
        ;;       
esac

if [ -z "$DOCKER_IMAGE" ]; then
    echo "Unrecognized target [$PACKAGE_OS.$PACKAGE_ARCH]" >&2
    exit 1
fi

case "$PACKAGE_OS.$PACKAGE_ARCH" in
    alpine.amd64)
        RUST_TARGET='x86_64-unknown-linux-musl'
        # The below SETUP was copied from https://github.com/emk/rust-musl-builder/blob/main/Dockerfile.
        SETUP_COMMAND=$'
            export DEBIAN_FRONTEND=noninteractive
            OPENSSL_VERSION=1.1.1i
            apt-get update && \
            apt-get install -y \
                build-essential \
                cmake \
                curl \
                file \
                git \
                musl-dev \
                musl-tools \
                libpq-dev \
                libsqlite-dev \
                libssl-dev \
                linux-libc-dev \
                pkgconf \
                sudo \
                xutils-dev \
                && \
            apt-get clean && rm -rf /var/lib/apt/lists/* && \
            useradd rust --user-group --create-home --shell /bin/bash --groups sudo && \
            echo "Building OpenSSL" && \
            ls /usr/include/linux && \
            sudo mkdir -p /usr/local/musl/include && \
            sudo ln -s /usr/include/linux /usr/local/musl/include/linux && \
            sudo ln -s /usr/include/x86_64-linux-gnu/asm /usr/local/musl/include/asm && \
            sudo ln -s /usr/include/asm-generic /usr/local/musl/include/asm-generic && \
            cd /tmp && \
            short_version="$(echo "$OPENSSL_VERSION" | sed s\'/[a-z]$//\' )" && \
            curl -fLO "https://www.openssl.org/source/openssl-$OPENSSL_VERSION.tar.gz" || \
                curl -fLO "https://www.openssl.org/source/old/$short_version/openssl-$OPENSSL_VERSION.tar.gz" && \
            tar xvzf "openssl-$OPENSSL_VERSION.tar.gz" && cd "openssl-$OPENSSL_VERSION" && \
            env CC=musl-gcc ./Configure no-shared no-zlib -fPIC --prefix=/usr/local/musl -DOPENSSL_NO_SECURE_MEMORY linux-x86_64 && \
            env C_INCLUDE_PATH=/usr/local/musl/include/ make depend && \
            env C_INCLUDE_PATH=/usr/local/musl/include/ make && \
            sudo make install && \
            sudo rm /usr/local/musl/include/linux /usr/local/musl/include/asm /usr/local/musl/include/asm-generic && \
            rm -r /tmp/*
            export OPENSSL_DIR=/usr/local/musl/
            export OPENSSL_INCLUDE_DIR=/usr/local/musl/include/
            export DEP_OPENSSL_INCLUDE=/usr/local/musl/include/
            export OPENSSL_LIB_DIR=/usr/local/musl/lib/
            export OPENSSL_STATIC=1
            export PQ_LIB_STATIC_X86_64_UNKNOWN_LINUX_MUSL=1
            export PG_CONFIG_X86_64_UNKNOWN_LINUX_GNU=/usr/bin/pg_config
            export PKG_CONFIG_ALLOW_CROSS=true
            export PKG_CONFIG_ALL_STATIC=true
            export LIBZ_SYS_STATIC=1
            export TARGET=musl
            cd /project/$BUILD_PATH &&
            echo \'Installing rustup\' &&
            curl -sSLf https://sh.rustup.rs | sh -s -- -y &&
            . ~/.cargo/env &&
        '
        MAKE_FLAGS="'CARGOFLAGS=$CARGOFLAGS --target x86_64-unknown-linux-musl'"
        MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/x86_64-unknown-linux-musl/release'"
        MAKE_FLAGS="$MAKE_FLAGS 'STRIP_COMMAND=strip'"        
        ;;

    debian12.arm32v7)
        RUST_TARGET='armv7-unknown-linux-gnueabihf'
        
        SETUP_COMMAND=$'
            export DEBIAN_FRONTEND=noninteractive
            export TZ=UTC
            dpkg --add-architecture armhf &&
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates curl debhelper file git make \
                gcc g++ \
                gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf \
                libcurl4-openssl-dev:armhf libssl-dev:armhf uuid-dev:armhf &&

            mkdir -p ~/.cargo &&
            echo \'[target.armv7-unknown-linux-gnueabihf]\' > ~/.cargo/config &&
            echo \'linker = "arm-linux-gnueabihf-gcc"\' >> ~/.cargo/config &&
            export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_LIB_DIR=/usr/lib/arm-linux-gnueabihf &&
            export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_INCLUDE_DIR=/usr/include &&

            cd /project/$BUILD_PATH &&
            echo \'Installing rustup\' &&
            curl -sSLf https://sh.rustup.rs | sh -s -- -y &&
            . ~/.cargo/env &&
        '
        MAKE_FLAGS="'CARGOFLAGS=$CARGOFLAGS --target armv7-unknown-linux-gnueabihf'"
        MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/armv7-unknown-linux-gnueabihf/release'"
        MAKE_FLAGS="$MAKE_FLAGS 'STRIP_COMMAND=/usr/arm-linux-gnueabihf/bin/strip'"
        ;;

    debian12.aarch64)
        RUST_TARGET='aarch64-unknown-linux-gnu'
        
        SETUP_COMMAND=$'
            export DEBIAN_FRONTEND=noninteractive
            export TZ=UTC
            dpkg --add-architecture arm64 &&
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates curl debhelper file git make \
                gcc g++ \
                gcc-aarch64-linux-gnu g++-aarch64-linux-gnu \
                libcurl4-openssl-dev:arm64 libssl-dev:arm64 uuid-dev:arm64 &&

            mkdir -p ~/.cargo &&
            echo \'[target.aarch64-unknown-linux-gnu]\' > ~/.cargo/config &&
            echo \'linker = "aarch64-linux-gnu-gcc"\' >> ~/.cargo/config &&
            export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_LIB_DIR=/usr/lib/aarch64-linux-gnu &&
            export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_INCLUDE_DIR=/usr/include &&

            cd /project/$BUILD_PATH &&
            echo \'Installing rustup\' &&
            curl -sSLf https://sh.rustup.rs | sh -s -- -y &&
            . ~/.cargo/env &&
        '
        MAKE_FLAGS="'CARGOFLAGS=$CARGOFLAGS --target aarch64-unknown-linux-gnu'"
        MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/aarch64-unknown-linux-gnu/release'"
        MAKE_FLAGS="$MAKE_FLAGS 'STRIP_COMMAND=/usr/aarch64-linux-gnu/bin/strip'"
        ;;
esac

if [ -n "$RUST_TARGET" ]; then
    RUST_TARGET_COMMAND="rustup target add $RUST_TARGET &&"
fi

if [ -z "$SETUP_COMMAND" ]; then
    echo "Unrecognized target [$PACKAGE_OS.$PACKAGE_ARCH]" >&2
    exit 1
fi

MAKE_COMMAND="make release $MAKE_FLAGS"


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
        cd /project/$BUILD_PATH &&
        # build artifacts
        $RUST_TARGET_COMMAND
        $MAKE_COMMAND
    "
