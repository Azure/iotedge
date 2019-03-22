ARG base_tag=2.1.6-runtime-alpine3.7
FROM azureiotedge/azureiotedge-runtime-base:1.0.0-preview001-linux-amd64 as builder

FROM microsoft/dotnet:${base_tag}

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
