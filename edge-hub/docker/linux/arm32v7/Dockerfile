ARG base_tag=3.1-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

# Add an unprivileged user account for running Edge Hub
ARG EDGEHUBUSER_ID=1000
RUN useradd -ms /bin/bash -u ${EDGEHUBUSER_ID} edgehubuser
ENV EdgeHubUser=edgehubuser

RUN apt-get update && apt-get install -y libcap2-bin libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

COPY $EXE_DIR/librocksdb/librocksdb.so.armhf /usr/lib/librocksdb.so

# add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting edge hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

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
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
