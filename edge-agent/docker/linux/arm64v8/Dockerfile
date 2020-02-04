ARG base_tag=1.0.3-linux-arm64v8

FROM azureiotedge/azureiotedge-agent-base:${base_tag}
 
ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Agent" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
