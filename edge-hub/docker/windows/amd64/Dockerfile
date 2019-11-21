ARG base_tag=2.1.13-nanoserver-1809
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]