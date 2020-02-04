#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
PROJECT_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
LIBIOTHSM_BUILD_DIR="$PROJECT_ROOT/target/hsm"

REVISION="${REVISION:-1}"
DEFAULT_VERSION="$(cat "$PROJECT_ROOT/version.txt")"
VERSION="${VERSION:-$DEFAULT_VERSION}"

CMAKE_ARGS='-DCMAKE_SYSTEM_NAME=Linux -DCMAKE_SYSTEM_VERSION=1 -DCMAKE_BUILD_TYPE=Release'
CMAKE_ARGS="$CMAKE_ARGS -DBUILD_SHARED=On -Drun_unittests=Off -Duse_default_uuid=On -Duse_emulator=Off -Duse_http=Off"

case "$PACKAGE_OS" in
    'centos7')
        # Converts debian versioning to rpm version
        # deb 1.0.1~dev100 ~> rpm 1.0.1-0.1.dev100
        RPM_VERSION="$(echo "$VERSION" | cut -d"~" -f1)"
        RPM_TAG="$(echo "$VERSION" | cut -s -d"~" -f2)"
        if [ -n "$RPM_TAG" ]; then
            RPM_RELEASE="0.$REVISION.$RPM_TAG"
        else
            RPM_RELEASE="$REVISION"
        fi

        case "$PACKAGE_ARCH" in
            'amd64')
                DOCKER_IMAGE='centos:7.5.1804'
                ;;

            # CentOS 7's base repo does not have cross-compiler packages.
            #
            # EPEL does have cross-compiler gcc and g++ packages, but not libc.
            # The general intent of upstream is that the cross-compilers are only used for kernel development.
            #
            # Fedora's repo does have cross-compiler libc as well, but it may not be usable
            # because of https://bugzilla.redhat.com/show_bug.cgi?id=1456209
            #
            # So stick with the linaro compiler for now.

            'arm32v7')
                DOCKER_IMAGE='azureiotedge/gcc-linaro-7.3.1-2018.05-x86_64_arm-linux-gnueabihf:centos_7.5-1'
                ;;

            'aarch64')
                DOCKER_IMAGE='azureiotedge/gcc-linaro-7.3.1-2018.05-x86_64_aarch64-linux-gnu:centos_7.5-1'
                ;;
        esac

        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=RPM"
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$RPM_VERSION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_RPM_PACKAGE_RELEASE=$RPM_RELEASE'"
        CMAKE_ARGS="$CMAKE_ARGS '-DOPENSSL_DEPENDS_SPEC=openssl-libs'"
        ;;

    'debian8')
        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=DEB"

        case "$PACKAGE_ARCH" in
            'amd64')
                DOCKER_IMAGE='debian:8-slim'

                # The cmake in this image doesn't understand CPACK_DEBIAN_PACKAGE_RELEASE, so include the REVISION in CPACK_PACKAGE_VERSION
                CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$VERSION-$REVISION'"
                ;;

            # Debian 8 doesn't have cross-compiler packages in its main repo
            #
            # The emdebian repos have cross-compiler packages but they cannot be installed because of broken dependencies.
            # >gcc-4.9-arm-linux-gnueabihf : Depends: libgcc-4.9-dev:armhf (= 4.9.2-10) but 4.9.2-10+deb8u2 is to be installed
            #
            # emdebian is also not maintained any more, not in the least because Debian 9+ have cross-compiler packages in the main repo.
            #
            # So stick with the linaro compiler for now.
            'arm32v7')
                DOCKER_IMAGE='azureiotedge/gcc-linaro-7.3.1-2018.05-x86_64_arm-linux-gnueabihf:debian_8.11-1'

                CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$VERSION'"
                CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_DEBIAN_PACKAGE_RELEASE=$REVISION'"
                ;;

            'aarch64')
                # Like the comment in packages.yaml says, Debian 8 aarch64 is not LTS, and doesn't have any official or ports repos.
                #
                # So don't build it at all.
                ;;
        esac

        CMAKE_ARGS="$CMAKE_ARGS '-DOPENSSL_DEPENDS_SPEC=libssl1.0.0'"
        ;;

    'debian9')
        DOCKER_IMAGE='debian:9-slim'

        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=DEB"
        # The cmake in this image doesn't understand CPACK_DEBIAN_PACKAGE_RELEASE, so include the REVISION in CPACK_PACKAGE_VERSION
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$VERSION-$REVISION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DOPENSSL_DEPENDS_SPEC=libssl1.1'"
        ;;

    'debian10')
        DOCKER_IMAGE='debian:10-slim'

        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=DEB"
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_PACKAGE_VERSION=$VERSION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DCPACK_DEBIAN_PACKAGE_RELEASE=$REVISION'"
        CMAKE_ARGS="$CMAKE_ARGS '-DOPENSSL_DEPENDS_SPEC=libssl1.1'"
        ;;

    'ubuntu16.04')
        DOCKER_IMAGE='ubuntu:16.04'

        CMAKE_ARGS="$CMAKE_ARGS -DCPACK_GENERATOR=DEB"
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
    centos7.amd64)
        SETUP_COMMAND=$'
            yum update -y &&
            yum install -y \
                cmake curl git make rpm-build \
                gcc gcc-c++ \
                libcurl-devel libuuid-devel openssl-devel &&
        '
        ;;

    centos7.arm32v7)
        SETUP_COMMAND=$'
            yum update -y &&

            mkdir -p ~/.cargo &&
            echo \'[target.armv7-unknown-linux-gnueabihf]\' > ~/.cargo/config &&
            echo \'linker = "arm-linux-gnu-gcc"\' >> ~/.cargo/config &&
        '
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_SYSROOT=/toolchain/arm-linux-gnueabihf/libc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=/toolchain/bin/arm-linux-gnueabihf-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=/toolchain/bin/arm-linux-gnueabihf-g++"
        ;;

    centos7.aarch64)
        SETUP_COMMAND=$'
            yum update -y &&

            mkdir -p ~/.cargo &&
            echo \'[target.aarch64-unknown-linux-gnu]\' > ~/.cargo/config &&
            echo \'linker = "aarch64-linux-gnu-gcc"\' >> ~/.cargo/config &&
        '
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_SYSROOT=/toolchain/aarch64-linux-gnu/libc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=/toolchain/bin/aarch64-linux-gnu-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=/toolchain/bin/aarch64-linux-gnu-g++"
        ;;

    debian*.amd64)
        SETUP_COMMAND=$'
            apt-get update &&
            apt-get upgrade -y &&
            apt-get install -y --no-install-recommends \
                binutils build-essential ca-certificates curl cmake debhelper dh-systemd file git make \
                gcc g++ pkg-config \
                libcurl4-openssl-dev libssl-dev uuid-dev &&
        '
        ;;

    debian8.arm32v7)
        SETUP_COMMAND=$'
            apt-get update &&
            apt-get upgrade -y &&

            mkdir -p ~/.cargo &&
            echo \'[target.armv7-unknown-linux-gnueabihf]\' > ~/.cargo/config &&
            echo \'linker = "arm-linux-gnueabihf-gcc"\' >> ~/.cargo/config &&
        '
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_SYSROOT=/toolchain/arm-linux-gnueabihf/libc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=/toolchain/bin/arm-linux-gnueabihf-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=/toolchain/bin/arm-linux-gnueabihf-g++"
        ;;

    debian*.arm32v7)
        SETUP_COMMAND=$'
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
            echo \'linker = "arm-linux-gnueabihf-gcc"\' >> ~/.cargo/config &&
            export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_LIB_DIR=/usr/lib/arm-linux-gnueabihf &&
            export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_INCLUDE_DIR=/usr/include &&
        '
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=arm-linux-gnueabihf-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=arm-linux-gnueabihf-g++"
        ;;

    debian*.aarch64)
        SETUP_COMMAND=$'
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
            echo \'linker = "aarch64-linux-gnu-gcc"\' >> ~/.cargo/config &&
            export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_LIB_DIR=/usr/lib/aarch64-linux-gnu &&
            export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_INCLUDE_DIR=/usr/include &&
        '
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=aarch64-linux-gnu-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=aarch64-linux-gnu-g++"
        ;;

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
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_C_COMPILER=aarch64-linux-gnu-gcc"
        CMAKE_ARGS="$CMAKE_ARGS -DCMAKE_CXX_COMPILER=aarch64-linux-gnu-g++"
        ;;
esac

if [ -z "$SETUP_COMMAND" ]; then
    echo "Unrecognized target [$PACKAGE_OS.$PACKAGE_ARCH]" >&2
    exit 1
fi

case "$PACKAGE_OS" in
    centos7)
        case "$PACKAGE_ARCH" in
            amd64)
                MAKE_TARGET_DIR='target/release'
                ;;
            arm32v7)
                MAKE_TARGET_DIR="target/$RUST_TARGET/release"
                CARGO_TARGET_FLAG="--target $RUST_TARGET"
                RPMBUILD_TARGET_FLAG='--target armv7hl'
                ;;
            aarch64)
                MAKE_TARGET_DIR="target/$RUST_TARGET/release"
                CARGO_TARGET_FLAG="--target $RUST_TARGET"
                RPMBUILD_TARGET_FLAG='--target aarch64'
                ;;
        esac

        MAKE_COMMAND="mkdir -p /project/edgelet/target/rpmbuild"
        MAKE_COMMAND="$MAKE_COMMAND && cd /project/edgelet/target/rpmbuild"
        MAKE_COMMAND="$MAKE_COMMAND && mkdir -p RPMS SOURCES SPECS SRPMS BUILD"
        MAKE_COMMAND="$MAKE_COMMAND && cd /project/edgelet"
        MAKE_COMMAND="$MAKE_COMMAND && make dist 'TARGET=target/rpmbuild/SOURCES' 'VERSION=$VERSION' 'REVISION=$REVISION'"
        MAKE_COMMAND="$MAKE_COMMAND && make rpm rpmbuilddir=/project/edgelet/target/rpmbuild 'TARGET=$MAKE_TARGET_DIR' 'VERSION=$VERSION' 'REVISION=$REVISION' 'CARGOFLAGS=--manifest-path ./Cargo.toml $CARGO_TARGET_FLAG' RPMBUILDFLAGS='-v -bb --clean --define \"_topdir /project/edgelet/target/rpmbuild\" $RPMBUILD_TARGET_FLAG'"
        ;;

    *)
        case "$PACKAGE_OS" in
            debian8)
                MAKE_TARGET='deb8'
                ;;
            *)
                MAKE_TARGET='deb'
                ;;
        esac

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

        MAKE_COMMAND="make $MAKE_TARGET 'VERSION=$VERSION' 'REVISION=$REVISION' $MAKE_FLAGS"
        ;;
esac


mkdir -p "$LIBIOTHSM_BUILD_DIR"

docker run --rm \
    --user root \
    -e 'USER=root' \
    -v "$BUILD_REPOSITORY_LOCALPATH:/project" \
    -i \
    "$DOCKER_IMAGE" \
    sh -c "
        set -e &&

        cat /etc/os-release &&

        $SETUP_COMMAND

        echo 'Installing rustup' &&
        curl -sSLf https://sh.rustup.rs | sh -s -- -y &&
        . ~/.cargo/env &&

        # libiothsm
        cd /project/edgelet/target/hsm &&
        cmake $CMAKE_ARGS /project/edgelet/hsm-sys/azure-iot-hsm-c/ &&
        make -j package &&

        # iotedged
        cd /project/edgelet &&
        $RUST_TARGET_COMMAND
        $MAKE_COMMAND
    "

# Some images use old CPACK which produces non-standard package filenames. This renames them.
LIBIOTHSM_PACKAGE_NAME='libiothsm-std'
case "$PACKAGE_OS" in
    'centos7')
        find "$LIBIOTHSM_BUILD_DIR" -maxdepth 1 -name "$LIBIOTHSM_PACKAGE_NAME-*-Linux.rpm" | while read -r f; do
            case "$PACKAGE_ARCH" in
                'amd64')
                    echo "Renaming package $(basename "$f") to $(basename "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${RPM_VERSION}-${RPM_RELEASE}.el7.x86_64.rpm")"
                    mv -f "$f" "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${RPM_VERSION}-${RPM_RELEASE}.el7.x86_64.rpm"
                    ;;

                'arm32v7')
                    echo "Renaming package $(basename "$f") to $(basename "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${RPM_VERSION}-${RPM_RELEASE}.el7.armv7hl.rpm")"
                    mv -f "$f" "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${RPM_VERSION}-${RPM_RELEASE}.el7.armv7hl.rpm"
                    ;;

                'aarch64')
                    echo "Renaming package $(basename "$f") to $(basename "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${RPM_VERSION}-${RPM_RELEASE}.el7.aarch64.rpm")"
                    mv -f "$f" "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${RPM_VERSION}-${RPM_RELEASE}.el7.aarch64.rpm"
                    ;;
            esac
        done
        ;;

    *)
        find "$LIBIOTHSM_BUILD_DIR" -maxdepth 1 -name "$LIBIOTHSM_PACKAGE_NAME-*-Linux.deb" | while read -r f; do
            case "$PACKAGE_ARCH" in
                'amd64')
                    echo "Renaming package $(basename "$f") to $(basename "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${VERSION}-${REVISION}_amd64.deb")"
                    mv -f "$f" "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${VERSION}-${REVISION}_amd64.deb"
                    ;;

                'arm32v7')
                    echo "Renaming package $(basename "$f") to $(basename "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${VERSION}-${REVISION}_armhf.deb")"
                    mv -f "$f" "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${VERSION}-${REVISION}_armhf.deb"
                    ;;

                'aarch64')
                    echo "Renaming package $(basename "$f") to $(basename "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${VERSION}-${REVISION}_arm64.deb")"
                    mv -f "$f" "$LIBIOTHSM_BUILD_DIR/${LIBIOTHSM_PACKAGE_NAME}_${VERSION}-${REVISION}_arm64.deb"
                    ;;
            esac
        done
        ;;
esac

find "$PROJECT_ROOT" -name '*.deb'
find "$PROJECT_ROOT" -name '*.rpm'
