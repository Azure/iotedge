# escape=`
FROM microsoft/windowsservercore:1709 AS installer-env

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

RUN New-SelfSignedCertificate -Subject ('CN={0}' -f $Env:ComputerName) -CertStoreLocation Cert:\CurrentUser\My; `
    CertUtil -ExportPFX -user -p `\"`\" my $Env:ComputerName \edge-server.pfx

FROM microsoft/dotnet:2.0.0-runtime-nanoserver-1709

ENV SSL_CERTIFICATE_PATH \certs
ENV SSL_CERTIFICATE_NAME edge-server.pfx

COPY --from=installer-env ["\\edge-server.pfx", "$SSL_CERTIFICATE_PATH\\$SSL_CERTIFICATE_NAME"]

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]
