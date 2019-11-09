ARG base_tag=1.0.0-preview013-linux-arm32v7
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "Analyzer.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

RUN apt-get update && apt-get install -y libcap2-bin libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

COPY librocksdb.so /usr/lib/

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet Analyzer.dll
