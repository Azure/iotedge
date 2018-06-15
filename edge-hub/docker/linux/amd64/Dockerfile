ARG base_tag=2.1.0-runtime-bionic
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/app/certs
ENV SSL_CERTIFICATE_NAME=mqtt-server.pfx

# RocksDB requires snappy
RUN apt-get update && \
    apt-get install -y libsnappy1v5 libcap2-bin && \
    ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so && \
    ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/x86_64-linux-gnu/libc.so && \
    rm -rf /var/lib/apt/lists/*

# Add an unprivileged user account for running Edge Hub
RUN useradd -ms /bin/bash edgehubuser

# Add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting Edge Hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

USER edgehubuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Edge Hub" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
