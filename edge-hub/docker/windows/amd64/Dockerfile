ARG base_tag=2.1.0-runtime-nanoserver-1803
FROM microsoft/dotnet:${base_tag}

ENV SSL_CERTIFICATE_PATH=c:\\mnt\\edgehub
ENV SSL_CERTIFICATE_NAME=edge-hub-server.cert.pfx

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]