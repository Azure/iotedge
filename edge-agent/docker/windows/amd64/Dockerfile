ARG base_tag=2.1.0-runtime-nanoserver-1803
FROM microsoft/dotnet:${base_tag}

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

ENV IPInterfaceName vEthernet (Ethernet)

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
