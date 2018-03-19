# escape=`
FROM microsoft/dotnet:2.0.6-runtime-nanoserver-1709

ENV SSL_CERTIFICATE_PATH=c:\mnt\edgehub
ENV SSL_CERTIFICATE_NAME=edge-hub-server.cert.pfx

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]
