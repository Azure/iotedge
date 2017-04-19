FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-armv7hf:latest

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/* ./

CMD dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
