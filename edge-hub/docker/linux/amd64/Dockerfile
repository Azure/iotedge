# -*- mode: dockerfile -*-

# You can override this `--build-arg BASE_IMAGE=...` to use different
# version of Rust or OpenSSL.
ARG BASE_IMAGE=ekidd/rust-musl-builder:stable

# Our first FROM statement declares the build environment.
FROM ${BASE_IMAGE} AS broker-builder

# Add our source code.
ADD ./mqtt ./mqtt

RUN sudo chown -R rust:rust ../src

# Build our application.
RUN cargo build --release --manifest-path ./mqtt/mqttd/Cargo.toml --no-default-features \
    && strip /home/rust/src/mqtt/target/x86_64-unknown-linux-musl/release/mqttd

RUN ls

RUN ls ./mqtt

FROM azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64 as hub-builder

# TODO: figure out why we cannot use arg directly
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.4-alpine3.11

ARG EXE_DIR=.	

# RocksDB requires snappy	
RUN apk update && \	
    apk add --no-cache snappy libcap	

# Add an unprivileged user account for running Edge Hub	
ARG EDGEHUBUSER_ID=1000	
RUN adduser -Ds /bin/sh -u ${EDGEHUBUSER_ID} edgehubuser 	

ENV RUST_LOG=info
EXPOSE 1883/tcp
EXPOSE 8883/tcp

COPY --from=broker-builder \
    /home/rust/src/mqtt/target/x86_64-unknown-linux-musl/release/mqttd \
    /usr/local/bin/

# Add the CAP_NET_BIND_SERVICE capability to the dotnet binary because	
# we are starting Edge Hub as a non-root user	
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet	

# Install RocksDB	
COPY --from=hub-builder publish/* /usr/local/lib/	

WORKDIR /app	

COPY $EXE_DIR/ ./	

# Expose MQTT, AMQP and HTTPS ports	
EXPOSE 8883/tcp	
EXPOSE 5671/tcp	
EXPOSE 443/tcp	

USER edgehubuser	

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \	
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
