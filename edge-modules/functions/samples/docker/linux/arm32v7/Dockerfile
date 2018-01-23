ARG buildno
FROM edgebuilds.azurecr.io/microsoft/azureiotedge-functions-binding:${buildno}-linux-arm32v7

WORKDIR /app

COPY $EXE_DIR/ ./$FUNCTION_FILE_NAME

CMD /usr/bin/dotnet WebJobs.Script.Host/WebJobs.Script.Host.dll ./$FUNCTION_FILE_NAME
