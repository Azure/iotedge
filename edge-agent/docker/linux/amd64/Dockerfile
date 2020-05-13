ARG base_tag=3.1.4-alpine3.11
FROM azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64 as builder

FROM mcr.microsoft.com/dotnet/core/aspnet:${base_tag}

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy

# Install RocksDB
COPY --from=builder publish/* /usr/local/lib/

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Agent" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
