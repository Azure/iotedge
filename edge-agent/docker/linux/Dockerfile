# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

ARG EXE_DIR=.
ARG TARGETPLATFORM

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy

# Install RocksDB
COPY $EXE_DIR/librocksdb/$TARGETPLATFORM/librocksdb.so /usr/local/lib/librocksdb.so

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
