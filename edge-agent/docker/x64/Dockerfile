FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-x64:latest

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/* ./

CMD ["/usr/bin/dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
