FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-armv7hf:latest

ARG EXE_DIR=.
ENV DockerLoggingDriver=journald

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
