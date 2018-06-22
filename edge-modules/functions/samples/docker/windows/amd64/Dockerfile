# escape=`

ARG buildno
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-functions-binding:${buildno}-windows-amd64

WORKDIR /app

COPY $EXE_DIR/ ./$FUNCTION_FILE_NAME

ENV AzureWebJobsScriptRoot=samples