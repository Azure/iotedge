FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-armv7hf:latest

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "Microsoft.Azure.Devices.Edge.Service.dll"]
