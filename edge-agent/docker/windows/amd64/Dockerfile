ARG base_tag=2.1.13-nanoserver-1809
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose metrics ports
EXPOSE 9600/tcp

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
