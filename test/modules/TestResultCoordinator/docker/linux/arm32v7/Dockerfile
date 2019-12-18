ARG base_tag=1.0.3-linux-arm32v7
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-module-base-rocksdb:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "TestResultCoordinator.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose HTTP port
EXPOSE 5001/tcp

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet TestResultCoordinator.dll
