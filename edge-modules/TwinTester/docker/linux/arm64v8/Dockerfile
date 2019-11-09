ARG base_tag=1.0.0-preview014-linux-arm64v8
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "TwinTester.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    apt-get install -y libcap2-bin && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so.2 && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libc.so.6 /usr/lib/aarch64-linux-gnu/libc.so.6

COPY  librocksdb.so /usr/li

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Module" && \
    exec /usr/bin/dotnet TwinTester.dll
