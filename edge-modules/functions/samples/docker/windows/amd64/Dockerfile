# escape=`

ARG buildno
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-functions-binding:${buildno}-windows-amd64

WORKDIR /app

COPY $EXE_DIR/ ./$FUNCTION_FILE_NAME

CMD dotnet WebJobs.Script.Host\WebJobs.Script.Host.dll $env:FUNCTION_FILE_NAME