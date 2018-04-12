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
TOOLCHAIN="stable-x86_64-unknown-linux-gnu"

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help          Print this help and exit."
    echo " -t, --toolchain     Toolchain (default: stable-x86_64-unknown-linux-gnu)"
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
            TOOLCHAIN="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--toolchain" ) save_next_arg=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

if command -v "$RUSTUP" >/dev/null; then
    $RUSTUP install "$TOOLCHAIN"
else
    curl https://sh.rustup.rs -sSf | sh -s -- -y --default-toolchain "$TOOLCHAIN"
fi

# Install OpenSSL
sudo apt-get update && \
    sudo apt-get install -y pkg-config libssl-dev
