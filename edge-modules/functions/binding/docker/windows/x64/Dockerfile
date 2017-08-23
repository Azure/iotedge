# escape=`

FROM edgebuilds.azurecr.io/azure-functions-runtime-windows-x64:2.0.0-beta1-0c7272b

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /app

COPY $EXE_DIR/ .\bindings

COPY .\windows\downloadAndRun.ps1 /app

ENV AzureWebJobs_ExtensionsPath /app

CMD .\downloadAndRun.ps1