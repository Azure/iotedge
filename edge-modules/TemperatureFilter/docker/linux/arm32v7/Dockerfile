FROM azureiotedge/azureiotedge-module-base:1.0-preview-linux-arm32v7

ARG EXE_DIR=.

ENV MODULE_NAME "TemperatureFilter.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD ["/usr/bin/dotnet", "TemperatureFilter.dll"]
