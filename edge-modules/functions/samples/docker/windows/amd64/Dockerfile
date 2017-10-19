# escape=`

FROM edgebuilds.azurecr.io/azedge-functions-binding-windows-amd64:preview-001

WORKDIR /app

COPY $EXE_DIR/ ./$FUNCTION_FILE_NAME

CMD dotnet WebJobs.Script.Host\WebJobs.Script.Host.dll $env:FUNCTION_FILE_NAME