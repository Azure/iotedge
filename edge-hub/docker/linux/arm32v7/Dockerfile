ARG base_tag=1.0.6.13-linux-arm32v7
FROM azureiotedge/azureiotedge-hub-base:${base_tag}

ADD ./watchdog/armv7-unknown-linux-gnueabihf/release/watchdog /usr/local/bin/watchdog
ADD ./mqtt/armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd
ADD ./mqtt/broker.json /app/mqttd/broker.json

WORKDIR /app

COPY Microsoft.Azure.Devices.Edge.Hub.Service/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 1883/tcp
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

ENV OptimizeForPerformance false
ENV MqttEventsProcessorThreadCount 1

COPY Microsoft.Azure.Devices.Edge.Hub.Service/ ./
COPY $EXE_DIR/Microsoft.Azure.Devices.Edge.Hub.Service/hubStart.sh ./

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \
    exec /app/hubStart.sh
