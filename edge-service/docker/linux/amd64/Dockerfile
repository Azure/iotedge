FROM microsoft/dotnet:2.0.0-runtime

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=edge-server.pfx

# required to expose these ports to external leaf devices
EXPOSE 443
EXPOSE 8883

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD ["scripts/linux/start-edge-service.sh"]
