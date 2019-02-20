ARG base_tag=3.0-runtime-bionic-arm64v8
ARG num_procs=4

FROM arm64v8/buildpack-deps:bionic as builder

RUN apt-get update && \
    apt-get install -y libsnappy-dev

ARG EXE_DIR=.

RUN mkdir -p ../publish && \ cp -vL ../native/arm/librocksdb.so ../publish/

FROM microsoft/dotnet:${base_tag}

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

COPY --from=builder publish/* /usr/lib/