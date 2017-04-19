FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-x64:latest

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/* ./

CMD dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
