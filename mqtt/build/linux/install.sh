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
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/mqtt

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
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
    $RUSTUP update "$(< "$PROJECT_ROOT/rust-toolchain")"
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
            * ) usage;;
        esac
    done
}

process_args "$@"

install_rust

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
