ARG base_tag=3.1.18-alpine3.13
# base dockerfile is located at \edge-util\docker\linux\amd64
FROM azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64 as builder

FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy libcap

ENV MODULE_NAME "TestResultCoordinator.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose HTTP port
EXPOSE 5001/tcp

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

# Install RocksDB
COPY --from=builder publish/* /usr/local/lib/

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet TestResultCoordinator.dll
