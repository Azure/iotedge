ARG base_tag=1.0.0-linux-arm32v7
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-module-base-rocksdb:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "Analyzer.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet Analyzer.dll
