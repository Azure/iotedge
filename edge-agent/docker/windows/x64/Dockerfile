FROM microsoft/dotnet:2.0.0-runtime-nanoserver

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
