#!/bin/bash

###############################################################################
# This script installs rustup
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename "$0")
RUSTUP="${CARGO_HOME:-"$HOME/.cargo"}/bin/rustup"
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
    echo " -p,  --package-arm            Add additional dependencies for armhf packaging"
    exit 1;
}

###############################################################################
# Install Rust
###############################################################################
function install_rust()
{
    if ! command -v "$RUSTUP" >/dev/null; then
        echo "Installing rustup"
        curl https://sh.rustup.rs -sSf | sh -s -- -y
    fi

    # Forcibly install the toolchain specified in the rust-toolchain file.
    #
    # rustup automatically installs a missing toolchain, so it would seem we don't have to do this.
    # However, Azure Devops VMs have stable pre-installed, and it's not necessarily latest stable.
    # If we let rustup auto-install the toolchain, it would continue to use the old pre-installed stable.
    #
    # We could check if the toolchain file contains "stable" and conditionally issue a `rustup update stable`,
    # but it's simpler to just always `update` whatever toolchain it is. `update` installs the toolchain
    # if it hasn't already been installed, so this also works for pinned versions.
    source $HOME/.cargo/env
    rustup update "$(< "$PROJECT_ROOT/rust-toolchain")"
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args()
{
    for arg in "$@"
    do
        case "$arg" in
            "-h" | "--help" ) usage;;
            "-p" | "--package-arm" ) ARM_PACKAGE=1;;
            * ) usage;;
        esac
    done
}

process_args "$@"

install_rust

# Install OpenSSL, curl and uuid and valgrind
sudo apt-get update || :
sudo apt-get install -y \
    pkg-config \
    uuid-dev curl \
    libcurl4-openssl-dev \
    libssl-dev \
    debhelper \
    dh-systemd \
    valgrind

if [[ -n "$ARM_PACKAGE" ]]; then
    # armhf cross tools for packaging
    # These packages need to be pinned to a specific version to make
    # the package dependent on the lowest version of glibc possible

    # Add trusty repo to get older libraries
    sudo add-apt-repository "deb http://archive.ubuntu.com/ubuntu/ trusty main universe"

    sudo apt-get update || :
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
        gcc-arm-linux-gnueabihf=4:4.8.2-1 \
        binutils-aarch64-linux-gnu

    # For future reference:
    # ubuntu systems (host) sets openssl library version to 1.0.0,
    # Debian (Jessie) systems (target) expects library version to be 1.0.0.
    # Debian (Stretch and later) systems (target) expects 1.0 library version to be 1.0.2.

    ${PROJECT_ROOT}/build/linux/copy-arm-libs.sh
fi
