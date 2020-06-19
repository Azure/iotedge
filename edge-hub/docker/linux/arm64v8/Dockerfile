ARG base_tag=1.0.5-linux-arm64v8
FROM azureiotedge/azureiotedge-hub-base:${base_tag}

ADD ./watchdog/arm64/watchdog /usr/local/bin/watchdog
ADD ./mqtt/arm64/mqttd /usr/local/bin/mqttd

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

USER edgehubuser
ENV OptimizeForPerformance false
ENV MqttEventsProcessorThreadCount 1
CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \
    /usr/local/bin/watchdog