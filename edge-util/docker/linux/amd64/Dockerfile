ARG num_procs=4

FROM alpine:3.7

# Build RocksDB for alpine
RUN apk update && \
    apk add build-base bash perl linux-headers coreutils git snappy-dev

RUN git clone -b v5.4.6 --depth 1 https://github.com/facebook/rocksdb.git && \
    cd rocksdb && \
    CFLAGS="-static-libstdc++ -Wno-psabi -DSNAPPY" PORTABLE=1 make -j ${num_procs} shared_lib && \
    strip librocksdb.so && \
    mkdir -p ../publish && \
    cp -vL librocksdb.so ../publish/