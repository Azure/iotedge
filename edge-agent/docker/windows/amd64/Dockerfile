ARG base_tag=3.1.4-nanoserver-1809
FROM mcr.microsoft.com/dotnet/core/aspnet:${base_tag}

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
