# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy

# Install RocksDB
COPY $EXE_DIR/librocksdb/librocksdb.so.armhf /usr/local/lib/librocksdb.so

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
