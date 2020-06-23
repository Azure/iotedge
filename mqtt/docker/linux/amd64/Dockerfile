# -*- mode: dockerfile -*-

# You can override this `--build-arg BASE_IMAGE=...` to use different
# version of Rust or OpenSSL.
ARG BASE_IMAGE=ekidd/rust-musl-builder:stable

# Our first FROM statement declares the build environment.
FROM ${BASE_IMAGE} AS builder

# Add our source code.
ADD . ./

# Force add tartget platform in case 
# rust-toolchain file contains version other than 'stable'.
# Otherwise it's noop.
RUN rustup target add x86_64-unknown-linux-musl

# Build our application.
RUN cargo build --release --manifest-path mqttd/Cargo.toml --no-default-features \
    && strip /home/rust/src/target/x86_64-unknown-linux-musl/release/mqttd

FROM busybox

# Add an unprivileged user account for running mqttd	
ARG MQTTDUSER_ID=1000

RUN adduser -Du ${MQTTDUSER_ID} mqttduser

ENV RUST_LOG=info
EXPOSE 1883/tcp
EXPOSE 8883/tcp

COPY --from=builder \
    /home/rust/src/target/x86_64-unknown-linux-musl/release/mqttd \
    /usr/local/bin/

USER mqttduser

ENTRYPOINT ["/usr/local/bin/mqttd"]