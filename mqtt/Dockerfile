# -*- mode: dockerfile -*-
#
# An example Dockerfile showing how to build a Rust executable using this
# image, and deploy it with a tiny Alpine Linux container.

# You can override this `--build-arg BASE_IMAGE=...` to use different
# version of Rust or OpenSSL.
ARG BASE_IMAGE=ekidd/rust-musl-builder:beta

# Our first FROM statement declares the build environment.
FROM ${BASE_IMAGE} AS builder

# Add our source code.
ADD . ./

# Fix permissions on source code.
# RUN sudo chown -R rust:rust /home/rust

# Build our application.
RUN cargo build -p mqttd --release && strip /home/rust/src/target/x86_64-unknown-linux-musl/release/mqttd

FROM scratch
ENV RUST_LOG=info
EXPOSE 1883/tcp
EXPOSE 8883/tcp

COPY --from=builder \
    /home/rust/src/target/x86_64-unknown-linux-musl/release/mqttd \
    /usr/local/bin/
ENTRYPOINT ["/usr/local/bin/mqttd"]
