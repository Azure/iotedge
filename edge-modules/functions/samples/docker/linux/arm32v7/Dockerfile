FROM edgebuilds.azurecr.io/azedge-functions-binding-arm32v7:preview-001

WORKDIR /app

COPY $EXE_DIR/ ./$FUNCTION_FILE_NAME

CMD /usr/bin/dotnet WebJobs.Script.Host/WebJobs.Script.Host.dll ./$FUNCTION_FILE_NAME
