FROM ubuntu:18.04 AS builder

ARG SRC_DIR=.

# Add source code
COPY $SRC_DIR/snitcher /snitcher

RUN sources="$(cat /etc/apt/sources.list | grep -E '^[^#]')" && \
    # Update existing repos to be specifically for amd64
    echo "$sources" | sed -e 's/^deb /deb [arch=amd64] /g' > /etc/apt/sources.list && \
    # Add armhf repos
    echo "$sources" | \
        sed -e 's/^deb /deb [arch=armhf] /g' \
            -e 's| http://archive.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g' \
            -e 's| http://security.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g' \
            >> /etc/apt/sources.list && \
    dpkg --add-architecture armhf && \
    apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y --no-install-recommends \
        binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git \
        gcc g++ \
        gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf \
        libcurl4-openssl-dev:armhf libssl-dev:armhf uuid-dev:armhf && \
    mkdir -p ~/.cargo && \
    echo '[target.armv7-unknown-linux-gnueabihf]' > ~/.cargo/config && \
    echo 'linker = "arm-linux-gnueabihf-gcc"' >> ~/.cargo/config && \
    export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_LIB_DIR=/usr/lib/arm-linux-gnueabihf && \
    export ARMV7_UNKNOWN_LINUX_GNUEABIHF_OPENSSL_INCLUDE_DIR=/usr/include && \
    # Install Rust
    curl -sSLf https://sh.rustup.rs | sh -s -- -y && \
        . ~/.cargo/env && \
        rustup target add armv7-unknown-linux-gnueabihf && \
    # Build app
    cd /snitcher && \
    $HOME/.cargo/bin/cargo build --release --target armv7-unknown-linux-gnueabihf

# Build real container
# [7/23/2020] Snitcher is on its way to be depricated. The base image is not migrated over to docker.io/azureiotedge
#   To whomever wants to update this base image will need to manually build & port over the base image
FROM edgebuilds.azurecr.io/microsoft/snitcher-base:1.0-linux-arm32v7
COPY --from=builder /snitcher/target/armv7-unknown-linux-gnueabihf/release/snitcher \
     /usr/local/bin/
CMD /usr/local/bin/snitcher