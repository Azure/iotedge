ARG base_tag=1.0.1-linux-arm32v7
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-module-base-rust:${base_tag}

WORKDIR /app

COPY ./armv7-unknown-linux-gnueabihf/release/generic-mqtt-tester ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /app/generic-mqtt-tester
