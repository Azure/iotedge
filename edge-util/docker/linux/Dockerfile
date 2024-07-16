# syntax=docker/dockerfile:1.4

# We don't need dotnet/runtime-deps, but we do need alpine. This is an easy way to get it from mcr.microsoft.com.
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine

ARG num_procs=4
ARG TARGETPLATFORM

# Build RocksDB for alpine
RUN apk update && \
    apk add build-base bash perl linux-headers coreutils git snappy-dev

RUN git clone -b v5.17.2 --depth 1 https://github.com/facebook/rocksdb.git && \
    cd rocksdb && \
    CFLAGS='-static-libstdc++ -Wno-psabi -Wno-error=deprecated-copy -Wno-error=pessimizing-move -Wno-error=redundant-move -DSNAPPY' \
        PORTABLE=1 \
        make -j ${num_procs} shared_lib && \
    strip librocksdb.so && \
    mkdir -p ../publish/$TARGETPLATFORM && \
    cp -vL ./librocksdb.so ../publish/$TARGETPLATFORM/
