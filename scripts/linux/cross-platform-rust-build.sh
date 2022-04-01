#!/bin/bash

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "This script is used for building a few rust components (i.e. broker, edge hub watchdog)."
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
    'ubuntu20.04')
        DOCKER_IMAGE='ubuntu:20.04'
        ;;

    'alpine')
        DOCKER_IMAGE='ubuntu:18.04'
        ;;       
esac

if [ -z "$DOCKER_IMAGE" ]; then
    echo "Unrecognized target [$PACKAGE_OS.$PACKAGE_ARCH]" >&2
    exit 1
fi

case "$PACKAGE_OS.$PACKAGE_ARCH" in
    ubuntu20.04.amd64)
        RUST_TARGET='x86_64-unknown-linux-gnu'

        SETUP_COMMAND=$'
            export DEBIAN_FRONTEND=noninteractive
            sources="$(cat /etc/apt/sources.list | grep -E \'^[^#]\')" &&
            # Update existing repos to be specifically for amd64
            echo "$sources" | sed -e \'s/^deb /deb [arch=amd64] /g\' > /etc/apt/sources.list

            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git make \
                gcc g++ libssl-dev pkg-config

            cd /project/$BUILD_PATH &&
            echo \'Installing rustup\' &&
            curl -sSLf https://sh.rustup.rs | sh -s -- -y &&
            . ~/.cargo/env &&
        '
        MAKE_FLAGS="'CARGOFLAGS=$CARGOFLAGS --target $RUST_TARGET'"
        MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/$RUST_TARGET/release'"
        MAKE_FLAGS="$MAKE_FLAGS 'STRIP_COMMAND=strip'"
        ;;

    alpine.amd64)
        RUST_TARGET='x86_64-unknown-linux-musl'
        # The below SETUP was copied from https://github.com/emk/rust-musl-builder/blob/main/Dockerfile.
        SETUP_COMMAND=$'
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
                gcc-multilib-arm-linux-gnueabihf \
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

    ubuntu20.04.arm32v7)
        RUST_TARGET='armv7-unknown-linux-gnueabihf'
        
        SETUP_COMMAND=$'
            export DEBIAN_FRONTEND=noninteractive
            sources="$(cat /etc/apt/sources.list | grep -E \'^[^#]\')" &&
            # Update existing repos to be specifically for amd64
            echo "$sources" | sed -e \'s/^deb /deb [arch=amd64] /g\' > /etc/apt/sources.list &&
            # Add armhf repos
            echo "$sources" |
                sed -e \'s/^deb /deb [arch=armhf] /g\' \
                    -e \'s| http://archive.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    -e \'s| http://security.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    >> /etc/apt/sources.list &&

            dpkg --add-architecture armhf &&
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git make \
                gcc g++ \
                gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf \
                libcurl4-openssl-dev:armhf libssl-dev:armhf uuid-dev:armhf &&

            mkdir -p ~/.cargo &&
            echo \'[target.armv7-unknown-linux-gnueabihf]\' > ~/.cargo/config &&
            echo \'linker = \"arm-linux-gnueabihf-gcc\"\' >> ~/.cargo/config &&
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

    alpine.arm32v7)
        RUST_TARGET='armv7-unknown-linux-musleabihf'

        SETUP_COMMAND=$'
        TOOLCHAIN=stable && \
        TARGET=armv7-unknown-linux-musleabihf && \
        OPENSSL_ARCH=linux-generic32 && \
        RUST_MUSL_CROSS_TARGET=$TARGET && \

        apt-get update && \
        apt-get install -y \
			build-essential \
			cmake \
			curl \
			file \
			git \
			sudo \
			xutils-dev \
			unzip \
			&& \
        apt-get clean && rm -rf /var/lib/apt/lists/* && \

echo \'OUTPUT = /usr/local/musl\r\nGCC_VER = 7.2.0\r\nDL_CMD = curl -C - -L -o\r\nCOMMON_CONFIG += CFLAGS=\"-g0 -Os\" CXXFLAGS=\"-g0 -Os\" LDFLAGS=\"-s\"\r\nCOMMON_CONFIG += --disable-nls\r\nGCC_CONFIG += --enable-languages=c,c++\r\nGCC_CONFIG += --disable-libquadmath --disable-decimal-float\r\nGCC_CONFIG += --disable-multilib\r\nCOMMON_CONFIG += --with-debug-prefix-map=$(CURDIR)=\r\n\' >  /tmp/config.mak &&
less /tmp/config.mak &&
cd /tmp && \
    curl -Lsq -o musl-cross-make.zip https://github.com/richfelker/musl-cross-make/archive/v0.9.8.zip && \
    unzip -q musl-cross-make.zip && \
    rm musl-cross-make.zip && \
    mv musl-cross-make-0.9.8 musl-cross-make && \
    cp /tmp/config.mak /tmp/musl-cross-make/config.mak && \
    cd /tmp/musl-cross-make && \
    TARGET=$TARGET make install > /tmp/musl-cross-make.log && \
    ln -s /usr/local/musl/bin/$TARGET-strip /usr/local/musl/bin/musl-strip && \
    cd /tmp && \
    rm -rf /tmp/musl-cross-make /tmp/musl-cross-make.log && 
	
    mkdir -p /home/rust/libs /home/rust/src &&
	
    export PATH=/root/.cargo/bin:/usr/local/musl/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin &&
    export TARGET_CC=$TARGET-gcc &&
    export TARGET_CXX=$TARGET-g++ &&
    export TARGET_C_INCLUDE_PATH=/usr/local/musl/$TARGET/include/ &&

    chmod 755 /root/ && \
    curl https://sh.rustup.rs -sqSf | \
    sh -s -- -y --default-toolchain $TOOLCHAIN && \
    rustup target add $TARGET &&
    echo \'[build]\ntarget = \"armv7-unknown-linux-musleabihf\"\n\n[target.armv7-unknown-linux-musleabihf]\nlinker = \"armv7-unknown-linux-musleabihf-gcc\"\n\' > /root/.cargo/config && \
 
    cd /home/rust/libs && \
    export CC=$TARGET_CC && \
    export C_INCLUDE_PATH=$TARGET_C_INCLUDE_PATH && \
    echo "Building zlib" && \
    VERS=1.2.11 && \
    CHECKSUM=c3e5e9fdd5004dcb542feda5ee4f0ff0744628baf8ed2dd5d66f8ca1197cb1a1 && \
    cd /home/rust/libs && \
    curl -sqLO https://zlib.net/zlib-$VERS.tar.gz && \
    echo "$CHECKSUM zlib-$VERS.tar.gz" > checksums.txt && \
    sha256sum -c checksums.txt && \
    tar xzf zlib-$VERS.tar.gz && cd zlib-$VERS && \
    ./configure --static --archs="-fPIC" --prefix=/usr/local/musl/$TARGET && \
    make && sudo make install && \
    cd .. && rm -rf zlib-$VERS.tar.gz zlib-$VERS checksums.txt && \
    echo "Building OpenSSL" && \
    VERS=1.0.2q && \
    CHECKSUM=5744cfcbcec2b1b48629f7354203bc1e5e9b5466998bbccc5b5fcde3b18eb684 && \
    curl -sqO https://www.openssl.org/source/openssl-$VERS.tar.gz && \
    echo "$CHECKSUM openssl-$VERS.tar.gz" > checksums.txt && \
    sha256sum -c checksums.txt && \
    tar xzf openssl-$VERS.tar.gz && cd openssl-$VERS && \
    ./Configure $OPENSSL_ARCH -fPIC --prefix=/usr/local/musl/$TARGET && \
    make depend && \
    make && sudo make install && \
    cd .. && rm -rf openssl-$VERS.tar.gz openssl-$VERS checksums.txt && \
    export OPENSSL_DIR=/usr/local/musl/$TARGET/ && \
    export OPENSSL_INCLUDE_DIR=/usr/local/musl/$TARGET/include/ && \
    export DEP_OPENSSL_INCLUDE=/usr/local/musl/$TARGET/include/ && \
    export OPENSSL_LIB_DIR=/usr/local/musl/$TARGET/lib/ && \
    export OPENSSL_STATIC=1 && \
        '

    MAKE_FLAGS="'CARGOFLAGS=$CARGOFLAGS --target armv7-unknown-linux-musleabihf'"
    MAKE_FLAGS="$MAKE_FLAGS 'TARGET=target/armv7-unknown-linux-musleabihf/release'"
    MAKE_FLAGS="$MAKE_FLAGS 'STRIP_COMMAND=musl-strip'"
        ;;

    ubuntu20.04.aarch64| alpine.aarch64)
        RUST_TARGET='aarch64-unknown-linux-gnu'
        
        SETUP_COMMAND=$'
            export DEBIAN_FRONTEND=noninteractive
            sources="$(cat /etc/apt/sources.list | grep -E \'^[^#]\')" &&
            # Update existing repos to be specifically for amd64
            echo "$sources" | sed -e \'s/^deb /deb [arch=amd64] /g\' > /etc/apt/sources.list &&
            # Add arm64 repos
            echo "$sources" |
                sed -e \'s/^deb /deb [arch=arm64] /g\' \
                    -e \'s| http://archive.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    -e \'s| http://security.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g\' \
                    >> /etc/apt/sources.list &&

            dpkg --add-architecture arm64 &&
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git make \
                gcc g++ \
                gcc-aarch64-linux-gnu g++-aarch64-linux-gnu \
                libcurl4-openssl-dev:arm64 libssl-dev:arm64 uuid-dev:arm64 &&

            mkdir -p ~/.cargo &&
            echo \'[target.aarch64-unknown-linux-gnu]\' > ~/.cargo/config &&
            echo \'linker = \"aarch64-linux-gnu-gcc\"\' >> ~/.cargo/config &&
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
