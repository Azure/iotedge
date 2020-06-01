ARG base_tag=3.1.4-nanoserver-1809
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

USER ContainerAdministrator

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "load-gen.dll"]

RUN REG add "HKLM\SYSTEM\CurrentControlSet\services\Dnscache" /v Start /t REG_DWORD /d 4 /f
