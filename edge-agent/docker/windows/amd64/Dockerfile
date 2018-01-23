# escape=`
FROM microsoft/windowsservercore:1709 AS installer-env

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

RUN Invoke-WebRequest https://edgeblddiag.blob.core.windows.net/dotnet/System.IO.Pipes.dll -OutFile \System.IO.Pipes.dll

FROM microsoft/dotnet:2.0.5-runtime-nanoserver-1709

USER ContainerAdministrator

RUN setx /M PATH "%PATH%;%ProgramFiles%\dotnet"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

ENV MNCA \Program Files\dotnet\shared\Microsoft.NETCore.App\2.0.5

COPY --from=installer-env ["\\System.IO.Pipes.dll", "$MNCA\\"]

ENV IPInterfaceName vEthernet (Ethernet)

CMD ["dotnet.exe", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
