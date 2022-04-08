ARG base_tag=6.0-focal-arm32v7
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

RUN apt-get update && apt-get install -y libcap2-bin libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

COPY $EXE_DIR/librocksdb/librocksdb.so.armhf /usr/lib/librocksdb.so

RUN apt-get update && apt-get install -y --no-install-recommends iproute2 net-tools

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

ARG EXE_DIR=.

ENV MODULE_NAME "TwinTester.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet TwinTester.dll
