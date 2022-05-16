# syntax=docker/dockerfile:1.4

FROM debian:stretch

RUN dpkg --add-architecture armhf && \
    apt-get update && apt-get install --no-install-recommends -y \
      curl \
      cmake \
      build-essential \
      ca-certificates \
      git \
      libssl-dev \
      gcc-arm-linux-gnueabihf \
      libc6-armhf-cross \
      libc6-dev-armhf-cross \
      libssl-dev:armhf && \
    apt-get clean && rm -rf /var/lib/apt/lists/* && \
    useradd rust --user-group --create-home --shell /bin/bash --groups sudo

# Set up our path with all our binary directories, including those for the
# Rust toolchain.
ENV PATH=/home/rust/.cargo/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin

# Run all further code as user `rust`, and create our working directories
# as the appropriate user.
USER rust
RUN mkdir -p /home/rust/libs /home/rust/src

# Install Rust toolchain
RUN curl https://sh.rustup.rs -sSf | \
    sh -s -- -y --default-toolchain stable && \
    rustup target add armv7-unknown-linux-gnueabihf

ENV CARGO_TARGET_ARMV7_UNKNOWN_LINUX_GNUEABIHF_LINKER=arm-linux-gnueabihf-gcc \
    OPENSSL_INCLUDE_DIR=/usr/include/openssl/ \
    OPENSSL_LIB_DIR=/usr/lib/arm-linux-gnueabihf/

WORKDIR /home/rust/src
