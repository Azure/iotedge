ARG base_tag=3.0.0-bionic-arm64v8
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    apt-get install -y libcap2-bin && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so.2 && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libc.so.6 /usr/lib/aarch64-linux-gnu/libc.so.6

COPY  librocksdb.so /usr/lib

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser
