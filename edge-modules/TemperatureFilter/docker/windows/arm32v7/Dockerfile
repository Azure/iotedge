ARG base_tag=1.0.0-windows-arm32v7
ARG registry
FROM ${registry}/azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser
CMD echo %DATE:~4,2%-%DATE:~7,2%-%DATE:~10,4% %TIME%: Starting Module && \
    dotnet TemperatureFilter.dll
