ARG base_tag=3.1.18-nanoserver-1809
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

# In order to set system PATH, ContainerAdministrator must be used
USER ContainerAdministrator
RUN setx /M PATH "%PATH%;%ProgramFiles%\\dotnet"
USER ContainerUser

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
