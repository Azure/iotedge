ARG base_tag=3.1.11-alpine3.12
FROM azureiotedge/azureiotedge-runtime-base:1.1-linux-amd64 as builder

FROM mcr.microsoft.com/dotnet/core/aspnet:${base_tag}

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy libcap

ENV MODULE_NAME "TestAnalyzer.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

# Install RocksDB
COPY --from=builder publish/* /usr/local/lib/

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet TestAnalyzer.dll
