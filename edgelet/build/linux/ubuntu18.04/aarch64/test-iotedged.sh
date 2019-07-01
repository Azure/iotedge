#!/bin/bash

set -e

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet
BUILD_DIR_REL="target/aarch64-unknown-linux-gnu/release"
BUILD_DIR="$PROJECT_ROOT/$BUILD_DIR_REL"

CARGO_HOME=${CARGO_HOME:-"$HOME/.cargo/"}
RUSTUP_HOME=${RUSTUP_HOME:-"$HOME/.rustup"}
IMAGE="edgebuilds.azurecr.io/gcc-linaro-7.4.1-2019.02-x86_64_aarch64-linux-gnu:ubuntu18.04-1"

REVISION=${REVISION:-1}
DEFAULT_VERSION=$(cat $PROJECT_ROOT/version.txt)
VERSION="${VERSION:-$DEFAULT_VERSION}"

docker pull "$IMAGE"

run_command()
{
    echo "$1"
    docker \
      run \
      --rm \
      -e "USER=root" \
      -e "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/toolchain/aarch64-linux-gnu/bin:/toolchain/bin:/cargo/bin" \
      -e "RUSTUP_HOME=/rustup" \
      -v "$PROJECT_ROOT/target:/target" \
      -v "$BUILD_REPOSITORY_LOCALPATH:/project" \
      -v "$CARGO_HOME:/cargo" \
      -v "$RUSTUP_HOME:/rustup" \
      -i "$IMAGE" \
      sh -c "$1"
}

# Ensure the armv7 toolchain is installed
rustup target add aarch64-unknown-linux-gnu
rustup component add rust-src

mkdir -p $BUILD_DIR
cd $PROJECT_ROOT && make dist TARGET=$BUILD_DIR_REL VERSION=${VERSION} REVISION=${REVISION}

COMMAND="cargo test --manifest-path=/project/edgelet/Cargo.toml --target aarch64-unknown-linux-gnu --all"
run_command "$COMMAND"
