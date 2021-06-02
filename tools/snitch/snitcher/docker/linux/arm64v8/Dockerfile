FROM ubuntu:18.04 AS builder

ARG SRC_DIR=.

# Add source code
COPY $SRC_DIR/snitcher /snitcher

RUN sources="$(cat /etc/apt/sources.list | grep -E '^[^#]')" && \
    # Update existing repos to be specifically for amd64
    echo "$sources" | sed -e 's/^deb /deb [arch=amd64] /g' > /etc/apt/sources.list && \
    # Add arm64 repos
    echo "$sources" | \
        sed -e 's/^deb /deb [arch=arm64] /g' \
            -e 's| http://archive.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g' \
            -e 's| http://security.ubuntu.com/ubuntu/ | http://ports.ubuntu.com/ubuntu-ports/ |g' \
            >> /etc/apt/sources.list && \
    dpkg --add-architecture arm64 && \
    apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y --no-install-recommends \
        binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git \
        gcc g++ \
        gcc-aarch64-linux-gnu g++-aarch64-linux-gnu \
        libcurl4-openssl-dev:arm64 libssl-dev:arm64 uuid-dev:arm64 && \
    mkdir -p ~/.cargo && \
    echo '[target.aarch64-unknown-linux-gnu]' > ~/.cargo/config && \
    echo 'linker = "aarch64-linux-gnu-gcc"' >> ~/.cargo/config && \
    export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_LIB_DIR=/usr/lib/aarch64-linux-gnu && \
    export AARCH64_UNKNOWN_LINUX_GNU_OPENSSL_INCLUDE_DIR=/usr/include && \
    # Install Rust
    curl -sSLf https://sh.rustup.rs | sh -s -- -y && \
        . ~/.cargo/env && \
        rustup target add aarch64-unknown-linux-gnu && \
    # Build app
    cd /snitcher && \
    $HOME/.cargo/bin/cargo build --release --target aarch64-unknown-linux-gnu
    
# Build real container
# [7/23/2020] Snitcher is on its way to be depricated. The base image is not migrated over to docker.io/azureiotedge
#   To whomever wants to update this base image will need to manually build & port over the base image
FROM edgebuilds.azurecr.io/microsoft/snitcher-base:1.0-linux-arm64v8
COPY --from=builder /snitcher/target/aarch64-unknown-linux-gnu/release/snitcher \
     /usr/local/bin/
CMD /usr/local/bin/snitcher