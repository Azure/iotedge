FROM azureiotedge/azureiotedge-agent-base:1.0.0-preview001-linux-arm32v7

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
