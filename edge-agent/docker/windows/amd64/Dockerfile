ARG base_tag=2.1-runtime-nanoserver-1709
FROM microsoft/dotnet:${base_tag}

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

ENV IPInterfaceName vEthernet (Ethernet)

CMD ["dotnet.exe", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
