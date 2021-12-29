ARG base_tag=3.1.19-bionic-arm64v8
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

# Add an unprivileged user account for running Edge Hub
# value was chosen as a large value to avoid a typical regular uid
ARG EDGEHUBUSER_ID=1000
RUN useradd -ms /bin/bash -u ${EDGEHUBUSER_ID} edgehubuser
ENV EdgeHubUser=edgehubuser

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    apt-get install -y libcap2-bin && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so.2 && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libc.so.6 /usr/lib/aarch64-linux-gnu/libc.so.6
    
COPY $EXE_DIR/librocksdb/librocksdb.so.arm64 /usr/lib/librocksdb.so

# add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting edge hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

ADD ./watchdog/aarch64-unknown-linux-gnu/release/watchdog /usr/local/bin/watchdog
ADD ./mqtt/aarch64-unknown-linux-gnu/release/mqttd /usr/local/bin/mqttd
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
