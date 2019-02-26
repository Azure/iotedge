#!/bin/bash

###############################################################################
# This script installs the rust toolchain
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename "$0")
RUSTUP="$HOME/.cargo/bin/rustup"
TOOLCHAIN="stable"
ARM_PACKAGE=
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -t,  --toolchain              Toolchain (default: stable)"
    echo " -p,  --package-arm            Add additional dependencies for armhf packaging"
    exit 1;
}

###############################################################################
# Install rust toolchain
#
#   @param[1] - toolchain; Rust toolchain to install; Required;
#   @param[2] - set_default_toolchain; Boolean to set the toolchain as the
#               default toolchain duing its installation; Required;
###############################################################################
function install_toolchain()
{
    toolchain="$1"
    set_default_toolchain="$2"

    if [[ $set_default_toolchain == "true" ]]; then
        cmd_default_toolchain="--default-toolchain"
    else
        cmd_default_toolchain=""
    fi

    echo "Installing rust toolchain $toolchain"
    if command -v "$RUSTUP" >/dev/null; then
        $RUSTUP install $toolchain
    else
        curl https://sh.rustup.rs -sSf | sh -s -- -y $cmd_default_toolchain $toolchain
    fi
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            TOOLCHAIN="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--toolchain" ) save_next_arg=1;;
                "-p" | "--package-arm" ) ARM_PACKAGE=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

# install specified toolchain
install_toolchain $TOOLCHAIN true

# Add trusty repo to get older version of libc6-armhf-cross
sudo add-apt-repository "deb http://archive.ubuntu.com/ubuntu/ trusty main universe"

# Install OpenSSL, curl and uuid and valgrind
sudo apt-get update || :
sudo apt-get install -y \
    pkg-config \
    uuid-dev curl \
    libcurl4-openssl-dev \
    debhelper \
    dh-systemd \
    valgrind
sudo apt-get remove --yes libssl-dev
sudo apt-get install --yes --target-release xenial-updates libssl-dev

if [[ -n "$ARM_PACKAGE" ]]; then
    # armhf cross tools for packaging
    # These packages need to be pinned to a specific version to make
    # the package dependent on the lowest version of glibc possible

    sudo apt-get install -y \
        binutils-arm-linux-gnueabihf=2.24-2ubuntu3cross1.98 \
        libsfasan0-armhf-cross=4.8.2-16ubuntu4cross0.11 \
        libsfgcc-4.8-dev-armhf-cross=4.8.2-16ubuntu4cross0.11 \
        libasan0-armhf-cross=4.8.2-16ubuntu4cross0.11 \
        libatomic1-armhf-cross=4.8.2-16ubuntu4cross0.11 \
        libgomp1-armhf-cross=4.8.2-16ubuntu4cross0.11 \
        libgcc1-armhf-cross=1:4.8.2-16ubuntu4cross0.11 \
        gcc-4.8-arm-linux-gnueabihf-base=4.8.2-16ubuntu4cross0.11 \
        libgcc-4.8-dev-armhf-cross=4.8.2-16ubuntu4cross0.11 \
        cpp-4.8-arm-linux-gnueabihf=4.8.2-16ubuntu4cross0.11 \
        gcc-4.8-arm-linux-gnueabihf=4.8.2-16ubuntu4cross0.11 \
        gcc-4.8-multilib-arm-linux-gnueabihf=4.8.2-16ubuntu4cross0.11 \
        libc6-armhf-cross=2.19-0ubuntu2cross1.104 \
        gcc-arm-linux-gnueabihf=4:4.8.2-1

    # For future reference:
    # ubuntu systems (host) sets openssl library version to 1.0.0,
    # Debian (Jessie) systems (target) expects library version to be 1.0.0.
    # Debian (Stretch and later) systems (target) expects 1.0 library version to be 1.0.2.

    ${PROJECT_ROOT}/build/linux/copy-arm-libs.sh
fi
