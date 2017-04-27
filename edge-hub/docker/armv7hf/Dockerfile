FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-armv7hf:latest

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=mqtt-server.pfx

WORKDIR /app

COPY $EXE_DIR/ ./

CMD /bin/sh scripts/start.sh
