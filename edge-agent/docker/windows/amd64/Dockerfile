# escape=`
FROM microsoft/dotnet:2.0.6-runtime-nanoserver-1709

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

ENV IPInterfaceName vEthernet (Ethernet)

CMD ["dotnet.exe", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
