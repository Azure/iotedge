# escape=`
FROM microsoft/windowsservercore AS installer-env

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

RUN New-SelfSignedCertificate -Subject ('CN={0}' -f $Env:ComputerName); `
    CertUtil -ExportPFX -p `\"`\" my $Env:ComputerName \edge-server.pfx

FROM microsoft/dotnet:2.0.0-preview1-runtime

ENV SSL_CERTIFICATE_PATH \certs
ENV SSL_CERTIFICATE_NAME edge-server.pfx

COPY --from=installer-env ["\\edge-server.pfx", "$SSL_CERTIFICATE_PATH\\$SSL_CERTIFICATE_NAME"]

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Service.dll"]
