ARG base_tag=1.0.5-linux-arm32v7
FROM azureiotedge/azureiotedge-hub-base:${base_tag}

ADD ./watchdog/target/armv7-unknown-linux-gnueabihf/release/watchdog /usr/local/bin/watchdog
ADD ./mqttd/target/armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

RUN mkdir -p /tmp/mqtt/config

ARG EXE_DIR=./Microsoft.Azure.Devices.Edge.Hub.Service

WORKDIR /app

COPY $EXE_DIR/ ./edge-hub

# Set broker log level
ENV RUST_LOG=debug

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 1883/tcp
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

USER edgehubuser
ENV OptimizeForPerformance false
ENV MqttEventsProcessorThreadCount 1
CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \
    cd /tmp/mqtt && \
    /usr/local/bin/watchdog -c /tmp/mqtt/config/config.json
