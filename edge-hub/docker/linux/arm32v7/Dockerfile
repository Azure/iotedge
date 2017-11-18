FROM microsoft/dotnet:2.0.0-runtime-stretch-arm32v7

ENV EdgeHubUser=root

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=mqtt-server.pfx

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD ["scripts/linux/start.sh"]
