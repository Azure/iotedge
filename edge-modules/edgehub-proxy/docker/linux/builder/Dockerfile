FROM debian:stretch

# The Rust toolchain to use when building our image.
ARG TOOLCHAIN=stable

# The OpenSSL version to use.
ARG OPENSSL_VERSION=1.1.1d

RUN apt-get update && \
    apt-get install -y \
        build-essential \
        cmake \
        curl \
        file \
        git \
        libssl-dev \
        linux-libc-dev \
        pkgconf \
        sudo \
        xutils-dev \
	gcc-arm-linux-gnueabihf \
        && \
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
    sh -s -- -y --default-toolchain $TOOLCHAIN && \
    rustup target add armv7-unknown-linux-gnueabihf
ADD cargo-config.toml /home/rust/.cargo/config

ENV CC=arm-linux-gnueabihf-gcc

# Build a static library version of OpenSSL for arm32v7
RUN echo "Building OpenSSL" && \
    env MACHINE=armv7 && \
    env ARCH=arm && \
    cd /home/rust && \
    curl -LO https://www.openssl.org/source/openssl-$OPENSSL_VERSION.tar.gz && \
    tar xzf openssl-$OPENSSL_VERSION.tar.gz && \
    cd openssl-$OPENSSL_VERSION && ./config shared && make && cd - && \
    rm openssl-$OPENSSL_VERSION.tar.gz

ENV OPENSSL_LIB_DIR=/home/rust/openssl-$OPENSSL_VERSION/ \
    OPENSSL_INCLUDE_DIR=/home/rust/openssl-$OPENSSL_VERSION/include \
    OPENSSL_STATIC=1

WORKDIR /home/rust/src