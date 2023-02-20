#!/bin/bash
#https://github.com/Azure/iotedge/blob/main/scripts/linux/cross-platform-rust-build.sh
###############################################################################
# This script builds a static binary of the api-proxy-module
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
ARCH=$(uname -m)
DIR=$(cd "$(dirname "$0")" && pwd)
SCRIPT_NAME=$(basename "$0")
BUILD_BINARIESDIRECTORY=
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    case "$ARCH" in
        'amd64'|'x86_64')  ARCH='amd64' ;;
        'arm32v7'|'armv7l') ARCH='arm32v7' ;;
        'arm64v8'|'aarch64') ARCH='arm64v8' ;;
        *) echo "Unsupported architecture '$ARCH'" && exit 1 ;;
    esac
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ''
    echo 'options'
    echo ' -h,  --help              Print this help and exit.'
    echo ' -t,  --target-arch       Target architecture: amd64|arm32v7|aarch64'
    echo ' --bin-dir                Output directory'
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [[ "$save_next_arg" -eq 1 ]]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        elif [[ "$save_next_arg" -eq 2 ]]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target-arch" ) save_next_arg=1;;
                "--bin-dir" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done

    BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH}
    if [[ ! -d "$BUILD_BINARIESDIRECTORY" ]]; then
        mkdir "${BUILD_BINARIESDIRECTORY}"
    fi
}

###############################################################################
# Build project and publish result
###############################################################################
build_project()
{
    # Build API Proxy inside a Rust container. We'd prefer all our arch-specific images to be
    # Alpine-based, but Rust doesn't publish an arm32v7 Alpine container so we use Debian for
    # that architecture.
    case "$ARCH" in
        'amd64')
            platform='linux/amd64'
            triple='x86_64-unknown-linux-musl'
            image='rust:1-alpine3.17'
            ;;
        'arm32v7')
            platform='linux/arm/v7'
            triple='armv7-unknown-linux-gnueabihf'
            image='rust:bullseye'
            ;;
        'arm64v8')
            platform='linux/arm64'
            triple='aarch64-unknown-linux-musl'
            image='rust:1-alpine3.17'
            ;;
    esac

    case "$ARCH" in
        'arm32v7')
            commands=(  # Debian
                'apt-get update && apt-get -y upgrade && apt-get -y autoremove'
                'apt-get -y install libssl-dev'
                'addgroup --gid $BUILD_GID --system builder'
                'adduser --disabled-password --system --shell /bin/sh --uid $BUILD_UID --ingroup builder builder'
                'runuser -u builder make release CARGOFLAGS="--target $BUILD_TRIPLE" TARGET="target/$BUILD_TRIPLE/release"'
            )
            ;;
        *)
            commands=(  # Alpine
                'apk update && apk add doas make musl-dev openssl-dev pkgconfig'
                'addgroup -g $BUILD_GID -S builder'
                'adduser -D -S -s /bin/sh -u $BUILD_UID -G builder builder'
                'echo "permit nopass keepenv setenv { PATH } root as builder" > /etc/doas.d/doas.conf'
                'doas -n -u builder make release CARGOFLAGS="--target $BUILD_TRIPLE" TARGET="target/$BUILD_TRIPLE/release"'
            )
            ;;
    esac

    cd "$BUILD_REPOSITORY_LOCALPATH"
    src=edge-modules/api-proxy-module
    vol=/usr/src/iotedge

    docker run \
        --rm \
        --env BUILD_GID="$(id -g)" \
        --env BUILD_UID="$(id -u)" \
        --env BUILD_TRIPLE="$triple" \
        --platform $platform \
        --volume "$PWD":$vol \
        --workdir $vol/$src \
        $image \
        sh -c "set -e; $(printf '%s; ' "${commands[@]}")"

    # Populate the directory that will become the context for building the Docker image. Contents:
    # - api-proxy binary
    # - nginx conf templates folder
    # - Dockerfile
    EXE_DOCKER_DIR="$BUILD_BINARIESDIRECTORY/publish/api-proxy-module/docker/$platform"
    mkdir -p "$EXE_DOCKER_DIR"
    cp "$src/target/$triple/release/api-proxy-module" "$EXE_DOCKER_DIR/"
    cp -r "$src/templates" "$EXE_DOCKER_DIR/../"

    case "$ARCH" in
        'arm32v7') cp "$src/docker/linux/arm32v7/Dockerfile" "$EXE_DOCKER_DIR/" ;;
        *) cp "$src/docker/linux/Dockerfile" "$EXE_DOCKER_DIR/../" ;;
    esac
}

###############################################################################
# Print given command and execute it
###############################################################################
execute()
{
    echo "\$ $*"
    "$@"
    echo
}
check_arch
process_args "$@"

build_project
