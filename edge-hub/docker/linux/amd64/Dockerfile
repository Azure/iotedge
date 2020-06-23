ARG base_tag=3.1.4-alpine3.11
FROM azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64 as hub-builder

FROM mcr.microsoft.com/dotnet/core/aspnet:${base_tag}

ADD ./watchdog/amd64/watchdog /usr/local/bin/watchdog
ADD ./mqtt/amd64/mqttd /usr/local/bin/mqttd

ARG EXE_DIR=.	

# RocksDB requires snappy	
RUN apk update && \	
    apk add --no-cache snappy libcap	

# Add an unprivileged user account for running Edge Hub	
ARG EDGEHUBUSER_ID=1000	
RUN adduser -Ds /bin/sh -u ${EDGEHUBUSER_ID} edgehubuser 	

# Add the CAP_NET_BIND_SERVICE capability to the dotnet binary because	
# we are starting Edge Hub as a non-root user	
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet	

# Install RocksDB	
COPY --from=hub-builder publish/* /usr/local/lib/	

WORKDIR /app	

# Copy edge hub artifacts and remove unnecessary items, leaving just edge hub core dlls
COPY $EXE_DIR/ ./

# Set broker log level
ENV RUST_LOG=info

# Expose MQTT, AMQP and HTTPS ports	
EXPOSE 1883/tcp
EXPOSE 8883/tcp	
EXPOSE 5671/tcp	
EXPOSE 443/tcp	

USER edgehubuser	

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \
    /usr/local/bin/watchdog
