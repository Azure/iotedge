FROM microsoft/dotnet:2.0.0-preview1-runtime

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=mqtt-server.pfx

WORKDIR /app

COPY $EXE_DIR/ ./

#CMD ["scripts/start.sh"]
CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]