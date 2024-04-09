# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

ARG EXE_DIR=.
ARG TARGETPLATFORM

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy libcap

# Add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting Edge Hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

# Install RocksDB
COPY $EXE_DIR/librocksdb/$TARGETPLATFORM/librocksdb.so /usr/local/lib/librocksdb.so

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 1883/tcp
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \
    exec /app/hubStart.sh
