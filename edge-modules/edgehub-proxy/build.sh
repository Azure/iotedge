#!/bin/bash

###############################################################################
# This script builds a static binary of the edgehub-proxy
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

IMAGE=edgehub-proxy:latest

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
    echo " -t,  --target-arch            Target architecture"
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
            IMAGE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            TARGET_ARCH="$arg"
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


###############################################################################
# Build
###############################################################################
echo ${PROJECT_ROOT}
if [ $TARGET_ARCH == "amd64" ]; then
	docker run --rm -it -v "${PROJECT_ROOT}":/home/rust/src ekidd/rust-musl-builder cargo build --release --manifest-path /home/rust/src/edge-modules/edgehub-proxy/Cargo.toml
	strip ${PROJECT_ROOT}/edge-modules/edgehub-proxy/target/x86_64-unknown-linux-musl/release/edgehub-proxy
elif [ $TARGET_ARCH == "arm32v7" ]; then
	# download and compile openssl for arm32v7
	export MACHINE=armv7
	export ARCH=arm
	export CC=arm-linux-gnueabihf-gcc
	cd /tmp
	wget https://www.openssl.org/source/openssl-1.1.1d.tar.gz
	tar xzf openssl-1.1.1d.tar.gz
	cd openssl-1.1.1d && ./config shared && make && cd -

	# build edgehub-proxy locally for arm32v7
	cd ${PROJECT_ROOT}/edge-modules/edgehub-proxy/
	cargo build --release --target=armv7-unknown-linux-gnueabihf
	
	arm-linux-gnueabihf-strip target/armv7-unknown-linux-gnueabihf/release/edgehub-proxy
fi

docker build -t ${IMAGE} -f ${PROJECT_ROOT}/edge-modules/edgehub-proxy/docker/linux/$TARGET_ARCH/Dockerfile ${PROJECT_ROOT}/edge-modules/edgehub-proxy/
