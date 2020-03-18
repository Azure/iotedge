ARG base_tag=2.1.13-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

COPY librocksdb.so /usr/lib/