ARG buildno
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-functions-binding:${buildno}-linux-arm32v7

WORKDIR /app

COPY samples/ samples/

ENV AzureWebJobsScriptRoot=samples
