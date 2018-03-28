ARG base_tag=1.0.0-preview002-linux-arm32v7
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "SimulatedTemperatureSensor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD ["/usr/bin/dotnet", "SimulatedTemperatureSensor.dll"]
