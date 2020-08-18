ARG base_tag=1.0.6.0-linux-arm32v7
FROM azureiotedge/azureiotedge-hub-base:${base_tag}

ADD ./watchdog/target/armv7-unknown-linux-gnueabihf/release/watchdog /usr/local/bin/watchdog
ADD ./mqtt/target/armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd
ADD ./mqtt/broker.json /app/mqttd/broker.json

WORKDIR /app

COPY Microsoft.Azure.Devices.Edge.Hub.Service/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 1883/tcp
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

USER edgehubuser
ENV OptimizeForPerformance false
ENV MqttEventsProcessorThreadCount 1
CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \
    /usr/local/bin/watchdog
