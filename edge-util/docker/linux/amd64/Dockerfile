# docker file for azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64
FROM alpine:3.10

ARG num_procs=4

# Build RocksDB for alpine
RUN apk update && \
    apk add build-base bash perl linux-headers coreutils git snappy-dev

RUN git clone -b v5.17.2 --depth 1 https://github.com/facebook/rocksdb.git && \
    cd rocksdb && \
    CFLAGS="-static-libstdc++ -Wno-psabi -DSNAPPY" PORTABLE=1 make -j ${num_procs} shared_lib && \
    strip librocksdb.so && \
    mkdir -p ../publish && \
    cp -vL librocksdb.so ../publish/