ARG base_tag=3.1-nanoserver-1809
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

USER ContainerAdministrator
RUN reg add "hklm\system\currentcontrolset\control\Windows Containers" /v CryptoSpecializeOverride /t REG_DWORD /d 1
CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]
