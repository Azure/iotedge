#!/bin/bash
#https://github.com/Azure/iotedge/blob/master/scripts/linux/cross-platform-rust-build.sh
###############################################################################
# This script builds a static binary of the api-proxy-module
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

SCRIPT_NAME=$(basename "$0")
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}

IMAGE=api-proxy-module:latest

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -i,  --image                  Image name"
    echo " -t,  --target-arch            Target architecture: amd64|arm32v7|aarch64"
    exit 1;
}

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [[ "$ARCH" == "amd64" ]]; then
        ARCH="amd64"
    elif [[ "$ARCH" == "arm32v7" ]]; then
        ARCH="arm32v7"
    elif [[ "$ARCH" == "aarch64" ]]; then
        ARCH="aarch64"
    else
        echo "Unsupported architecture"
        exit 1
    fi
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
            IMAGE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            ARCH="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-i" | "--image" ) save_next_arg=1;;
                "-t" | "--target-arch" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"
check_arch


###############################################################################
# Build
###############################################################################

echo ${PROJECT_ROOT}



if [[ "$ARCH" == "amd64" ]]; then
set +e
../../scripts/linux/cross-platform-rust-build.sh --os alpine --arch $ARCH --build-path edge-modules/api-proxy-module
set -e

cp -r ./templates/ ./docker/linux/amd64
cp -r ./target/x86_64-unknown-linux-musl/release/api-proxy-module ./docker/linux/amd64
docker build . -t  azureiotedge-api-proxy -f docker/linux/amd64/Dockerfile
elif [[ "$ARCH" == "arm32v7" ]]; then

docker run --rm -it -v "${PROJECT_ROOT}":/home/rust/src messense/rust-musl-cross:armv7-musleabihf  /bin/bash -c " rm -frv ~/.rustup/toolchains/* &&curl -sSLf https://sh.rustup.rs | sh -s -- -y && rustup target add armv7-unknown-linux-musleabihf && cargo build --target=armv7-unknown-linux-musleabihf --release --manifest-path /home/rust/src/edge-modules/api-proxy-module/Cargo.toml"
cp -r ./templates/ ./docker/linux/arm32v7
cp -r ./target/armv7-unknown-linux-musleabihf/release/api-proxy-module ./docker/linux/arm32v7
docker build . -t  azureiotedge-api-proxy -f docker/linux/arm32v7/Dockerfile
elif [[ "$ARCH" == "aarch64" ]]; then
set +e
../../scripts/linux/cross-platform-rust-build.sh --os alpine --arch $ARCH --build-path edge-modules/api-proxy-module
set -e

cp -r ./templates/ ./docker/linux/arm64v8
cp -r ./target/aarch64-unknown-linux-gnu/release/api-proxy-module ./docker/linux/arm64v8
docker build . -t  azureiotedge-api-proxy -f docker/linux/arm64v8/Dockerfile
fi
