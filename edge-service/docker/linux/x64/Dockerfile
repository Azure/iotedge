FROM microsoft/dotnet:2.0.0-preview2-runtime-stretch

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=edge-server.pfx

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["scripts/linux/start-edge-service.sh"]
