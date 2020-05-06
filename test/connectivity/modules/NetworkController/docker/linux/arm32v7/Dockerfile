ARG base_tag=1.0.0-linux-arm32v7
FROM azureiotedge/azureiotedge-networkcontroller-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "NetworkController.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet NetworkController.dll
