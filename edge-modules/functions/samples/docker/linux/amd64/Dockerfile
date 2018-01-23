ARG buildno
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-functions-binding:${buildno}-linux-amd64

WORKDIR /app

COPY samples/ samples/

ENV AzureWebJobsScriptRoot=samples
