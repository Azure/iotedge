ARG base_tag=2.1.0-rc1-runtime-nanoserver-1709
FROM microsoft/dotnet:${base_tag}

ENV SSL_CERTIFICATE_PATH=c:\\mnt\\edgehub
ENV SSL_CERTIFICATE_NAME=edge-hub-server.cert.pfx

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD echo %DATE:~4,2%-%DATE:~7,2%-%DATE:~10,4% %TIME%: Starting Edge Hub && \
    dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
