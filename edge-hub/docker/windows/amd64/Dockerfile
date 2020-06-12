# -*- mode: dockerfile -*-

# You can override this `--build-arg BASE_IMAGE=...` to use different
# version of Rust or OpenSSL.
ARG BASE_IMAGE=rustembedded/cross:x86_64-pc-windows-gnu

# Our first FROM statement declares the build environment.
FROM ${BASE_IMAGE} AS broker-builder

# Add source code for mqtt broker and watchdog.
ADD ./mqtt ./mqtt
ADD ./watchdog ./watchdog

RUN sudo chown -R rust:rust ../src

#Build mqtt broker and watchdog.
RUN cross build --target x86_64-pc-windows-gnu --release --manifest-path ./mqtt/mqttd/Cargo.toml --no-default-features \
    && strip /home/rust/src/mqtt/target/x86_64-unknown-linux-musl/release/mqttd

RUN cross build --target x86_64-pc-windows-gnu --release --manifest-path ./watchdog/linux/Cargo.toml --no-default-features \
    && strip /home/rust/src/watchdog/linux/target/x86_64-unknown-linux-musl/release/watchdog

ARG base_tag=3.1.4-nanoserver-1809
FROM mcr.microsoft.com/dotnet/core/aspnet:${base_tag}

# TODO: Edit copy path
COPY --from=broker-builder \
    /home/rust/src/mqtt/target/x86_64-unknown-linux-musl/release/mqttd \
    /usr/local/bin/

COPY --from=broker-builder \
    /home/rust/src/watchdog/linux/target/x86_64-unknown-linux-musl/release/watchdog \
    /usr/local/bin/

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]