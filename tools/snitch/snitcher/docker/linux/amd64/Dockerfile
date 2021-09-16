FROM ubuntu:18.04 AS builder

ARG SRC_DIR=.

# Add source code
COPY $SRC_DIR/snitcher /snitcher
COPY $SRC_DIR/Cargo.lock /snitcher/Cargo.lock

# Install pre-requisite
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y --no-install-recommends \
    binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git \
    gcc g++ pkg-config \
    libcurl4-openssl-dev libssl-dev uuid-dev && \
    # Install Rust
    curl -sSLf https://sh.rustup.rs | sh -s -- -y && \
    . ~/.cargo/env && \
    # Build app
    cd /snitcher && \
    $HOME/.cargo/bin/cargo build --release

# Build real container
FROM ubuntu:18.04
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y ca-certificates openssl
COPY --from=builder /snitcher/target/release/snitcher \
    /usr/local/bin/
CMD /usr/local/bin/snitcher
