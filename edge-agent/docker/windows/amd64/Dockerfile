ARG base_tag=2.1.0-runtime-nanoserver-1803
FROM microsoft/dotnet:${base_tag}

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

ENV IPInterfaceName vEthernet (Ethernet)

CMD echo %DATE:~4,2%-%DATE:~7,2%-%DATE:~10,4% %TIME%: Starting Edge Agent && \
    dotnet Microsoft.Azure.Devices.Edge.Agent.Service.dll
