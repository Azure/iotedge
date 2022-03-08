ARG base_tag=3.1-nanoserver-1809
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

USER ContainerAdministrator

RUN REG add "HKLM\SYSTEM\CurrentControlSet\services\Dnscache" /v Start /t REG_DWORD /d 4 /f

USER ContainerUser

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"]
