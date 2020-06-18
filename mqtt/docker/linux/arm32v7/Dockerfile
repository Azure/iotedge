# -*- mode: dockerfile -*-

# You can override this `--build-arg BASE_IMAGE=...` to use different
# version of Rust or OpenSSL.
ARG BASE_IMAGE=messense/rust-musl-cross:armv7-musleabihf

# Our first FROM statement declares the build environment.
FROM ${BASE_IMAGE} AS builder

# Add our source code.
ADD . ./

# Force add tartget platform in case 
# rust-toolchain file contains version other than 'stable'.
# Otherwise it's noop.
RUN rustup target add armv7-unknown-linux-musleabihf

# Build our application.
RUN cargo build --release --manifest-path mqttd/Cargo.toml --no-default-features \ 
    && musl-strip /home/rust/src/target/armv7-unknown-linux-musleabihf/release/mqttd

FROM busybox

# Add an unprivileged user account for running mqttd	
ARG MQTTDUSER_ID=1000

RUN adduser -Du ${MQTTDUSER_ID} mqttduser

ENV RUST_LOG=info
EXPOSE 1883/tcp
EXPOSE 8883/tcp

COPY --from=builder \
    /home/rust/src/target/armv7-unknown-linux-musleabihf/release/mqttd \
    /usr/local/bin/

USER mqttduser

ENTRYPOINT ["/usr/local/bin/mqttd"]
