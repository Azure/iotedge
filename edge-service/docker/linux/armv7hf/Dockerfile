FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-armv7hf:latest

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=edge-server.pfx
ENV DockerLoggingDriver=journald

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["scripts/linux/start-edge-service.sh"]
