# syntax=docker/dockerfile:1.4

FROM alpine:3.14

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
