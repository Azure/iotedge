# docker file for azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64
# This underlying image is recommended to be the same as the ones for edgeAgent & edgeHub
# See the discussion at https://github.com/Azure/iotedge/pull/5965/files#r779936202
ARG base_tag=3.1-alpine
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG num_procs=4

# Build RocksDB for alpine
RUN apk update && \
    apk add build-base bash perl linux-headers coreutils git snappy-dev

RUN git clone -b v5.17.2 --depth 1 https://github.com/facebook/rocksdb.git && \
    cd rocksdb && \
    CFLAGS="-static-libstdc++ -Wno-psabi -Wno-error=deprecated-copy -Wno-error=pessimizing-move -Wno-error=redundant-move -DSNAPPY" PORTABLE=1 make -j ${num_procs} shared_lib && \
    strip librocksdb.so && \
    mkdir -p ../publish && \
    cp -vL librocksdb.so ../publish/librocksdb.so.amd64
