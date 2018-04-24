ARG base_tag=2.1-runtime-stretch-slim
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

# Install snappy and set up symlinks that are absent from the base image
# Required by RocksDb
RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so && \
    ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/x86_64-linux-gnu/libc.so && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Edge Agent" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
