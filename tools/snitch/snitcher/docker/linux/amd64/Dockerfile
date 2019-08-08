FROM ubuntu:18.04 AS builder

ARG SRC_DIR=./

# Install pre-requisite
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y --no-install-recommends \
    binutils build-essential ca-certificates cmake curl debhelper dh-systemd file git \
    gcc g++ pkg-config \
    libcurl4-openssl-dev libssl-dev uuid-dev
    # Install Rust
    curl -sSLf https://sh.rustup.rs | sh -s -- -y && \
    . ~/.cargo/env

# Add source code
COPY $SRC_DIR/snitcher /snitcher

# Build app
RUN cd /snitcher && $HOME/.cargo/bin/cargo build --release

# Build real container
FROM ubuntu:18.04    
COPY --from=builder /snitcher/target/release/snitcher \
     /usr/local/bin/
CMD /usr/local/bin/snitcher