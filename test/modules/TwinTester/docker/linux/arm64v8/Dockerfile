# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy libcap

ENV MODULE_NAME "TwinTester.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Install RocksDB
COPY $EXE_DIR/librocksdb/librocksdb.so.arm64 /usr/local/lib/librocksdb.so

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Module" && \
    exec /usr/bin/dotnet TwinTester.dll
