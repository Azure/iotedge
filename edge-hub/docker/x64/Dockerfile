FROM edgebuilds.azurecr.io/azedge-dotnet-runtime-x64:latest

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs

WORKDIR /app

COPY $EXE_DIR/* ./

CMD /bin/sh start.sh
