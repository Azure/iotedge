ARG base_tag=2.1.6-runtime-nanoserver-1809
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

USER ContainerAdministrator
# Add an unprivileged user account for running Edge Hub
RUN net user /add edgehubuser

USER edgehubuser
CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]